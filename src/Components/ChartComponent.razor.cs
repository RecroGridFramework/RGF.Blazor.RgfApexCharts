using ApexCharts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor;
using Recrovit.RecroGridFramework.Client.Blazor.Components;
using Recrovit.RecroGridFramework.Client.Blazor.Parameters;
using Recrovit.RecroGridFramework.Client.Events;
using Recrovit.RecroGridFramework.Client.Handlers;

namespace Recrovit.RecroGridFramework.Blazor.RgfApexCharts.Components;

public partial class ChartComponent : ComponentBase
{
    [Parameter, EditorRequired]
    public RgfEntityParameters EntityParameters { get; set; } = null!;

    [Inject]
    protected IJSRuntime _jsRuntime { get; set; } = default!;

    [Inject]
    protected IRecroDictService RecroDict { get; set; } = null!;

    protected RgfChartComponent _rgfChartRef { get; set; } = null!;

    protected ApexChartComponent _chartRef { get; set; } = null!;

    protected DotNetObjectReference<ChartComponent>? _selfRef;

    protected static int _id;

    protected string _containerId;

    protected EditContext _editContext = null!;

    protected ValidationMessageStore _messageStore = null!;

    protected IRgManager _manager => EntityParameters.Manager!;

    protected RgfChartParameters ChartParameters => EntityParameters.ChartParameters;

    protected ApexChartSettings _apexChartSettings { get; set; } = new();

    public ChartComponent()
    {
        _id = 1;
        _containerId = $"rgf-chart-{_id}";
    }

    protected override void OnInitialized()
    {
        EntityParameters.ChartParameters.EventDispatcher.Subscribe(RgfChartEventKind.ShowChart, (arg) => OnShowChart());

        var aggregationSettings = ChartParameters.AggregationSettings;
        aggregationSettings.MaxResults = 100;
        aggregationSettings.Columns = new List<RgfAggregationColumn>
        {
            new() { PropertyId = 0, Aggregate = "Count" }
        };

        _editContext = new(aggregationSettings);
        _editContext.OnValidationRequested += HandleValidationRequested;
        _messageStore = new(_editContext);

        _apexChartSettings.Options = new()
        {
            Theme = new Theme { Mode = Mode.Light, Palette = PaletteType.Palette1 },
            Chart = new() { Stacked = ChartParameters.Stacked },
            NoData = new NoData { Text = "No Data..." },
            PlotOptions = new PlotOptions()
            {
                Bar = new PlotOptionsBar()
                {
                    Horizontal = ChartParameters.Horizontal,
                    DataLabels = new PlotOptionsBarDataLabels { Total = new BarTotalDataLabels { Style = new BarDataLabelsStyle { FontWeight = "800" } } }
                }
            },
            DataLabels = new DataLabels
            {
                Enabled = true,
                Formatter = "function (value) { return Array.isArray(value) ? value.join('/') : value.toLocaleString(); }"
            },
            Yaxis = new List<YAxis>()
            {
                new YAxis { Labels = new YAxisLabels { Formatter = "function (value) { return Array.isArray(value) ? value.join('/') : value.toLocaleString(); }" } }
            }
        };
    }

    protected virtual async Task OnShowChart()
    {
        bool inited = false;
        var jquiVer = await RgfBlazorConfiguration.ChkJQueryUiVer(_jsRuntime);
        if (jquiVer >= 0)
        {
            _selfRef ??= DotNetObjectReference.Create(this);
            inited = await _jsRuntime.InvokeAsync<bool>($"{RgfApexChartsConfiguration.JsApexChartsNamespace}.initialize", _containerId, _selfRef);
        }
        if (!inited)
        {
            _apexChartSettings.Height = 200;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (firstRender)
        {
            await OnShowChart();
        }
    }

    [JSInvokable]
    public async Task Resize(int width, int height)
    {
        _apexChartSettings.Width = width < 1 ? null : Math.Max(width, 100);
        _apexChartSettings.Height = height < 1 ? null : Math.Max(height, 100);
        StateHasChanged();
        if (_rgfChartRef.IsStateValid && _rgfChartRef.ChartData.Count > 0)
        {
            await UpdateChart();
        }
    }

    protected void HandleValidationRequested(object? sender, ValidationRequestedEventArgs e) => _rgfChartRef.Validation(_messageStore, ChartParameters);

    protected virtual async Task<bool> Submit()
    {
        if (!_editContext.Validate())
        {
            return false;
        }
        var toast = RgfToastEvent.CreateActionEvent(RecroDict.GetRgfUiString("Request"), _manager.EntityDesc.MenuTitle, "RecroChart");
        await _manager.ToastManager.RaiseEventAsync(toast, this);
        var success = await _rgfChartRef.CreateChartDataAsyc(ChartParameters.AggregationSettings);
        if (!success)
        {
            return false;
        }
        await _manager.ToastManager.RaiseEventAsync(RgfToastEvent.RecreateToastWithStatus(toast, RecroDict.GetRgfUiString("Processed"), RgfToastType.Success, delay: 2), this);
        await UpdateChart(true);
        return true;
    }

    protected async Task UpdateChart(bool recreate = false)
    {
        if (!_rgfChartRef.IsStateValid)
        {
            await _manager.ToastManager.RaiseEventAsync(new RgfToastEvent(RecroDict.GetRgfUiString("Warning"), "Invalid state!", RgfToastType.Warning), this);
            return;
        }
        RgfToastEvent toast;
        if (recreate)
        {
            toast = RgfToastEvent.CreateActionEvent(RecroDict.GetRgfUiString("Request"), _manager.EntityDesc.MenuTitle, "Render");
            await _manager.ToastManager.RaiseEventAsync(toast, this);
            await _chartRef.RenderChartAsync($"{_manager.EntityDesc.Title} : ", ChartParameters.AggregationSettings, _rgfChartRef.DataColumns, _rgfChartRef.ChartData);
        }
        else
        {
            toast = RgfToastEvent.CreateActionEvent(RecroDict.GetRgfUiString("Request"), _manager.EntityDesc.MenuTitle, RecroDict.GetRgfUiString("Redraw"));
            await _manager.ToastManager.RaiseEventAsync(toast, this);
            await _chartRef.UpdateChart();
        }
        await _manager.ToastManager.RaiseEventAsync(RgfToastEvent.RecreateToastWithStatus(toast, RecroDict.GetRgfUiString("Processed"), RgfToastType.Success), this);
    }

    protected Task ChangeChartType(RgfChartSeriesType seriesType)
    {
        ChartParameters.SeriesType = seriesType;
        switch (seriesType)
        {
            case RgfChartSeriesType.Bar:
                _apexChartSettings.SeriesType = SeriesType.Bar;
                break;

            case RgfChartSeriesType.Line:
                _apexChartSettings.SeriesType = SeriesType.Line;
                break;

            case RgfChartSeriesType.Pie:
                _apexChartSettings.SeriesType = SeriesType.Pie;
                break;

            case RgfChartSeriesType.Donut:
                _apexChartSettings.SeriesType = SeriesType.Donut;
                break;
        }
        return UpdateChart(true);
    }

    protected Task ChangedLegend(bool value)
    {
        ChartParameters.Legend = value;
        _apexChartSettings.Options.Legend = !value ? default : new Legend
        {
            Formatter = @"function(seriesName, opts) { return [seriesName, ' - ', opts.w.globals.series[opts.seriesIndex].toLocaleString()] }"
        };
        return UpdateChart();
    }
}