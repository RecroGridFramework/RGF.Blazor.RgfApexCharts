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

public abstract class BaseChartComponent : ComponentBase
{
    [Parameter, EditorRequired]
    public RgfEntityParameters EntityParameters { get; set; } = null!;

    public BaseChartComponent()
    {
        _id++;
        ContainerId = $"rgf-apexchart-{_id}";
    }

    [Inject]
    protected IJSRuntime _jsRuntime { get; init; } = default!;

    [Inject]
    protected IRecroDictService RecroDict { get; init; } = null!;

    protected RgfChartComponent RgfChartRef { get; set; } = null!;

    protected ApexChartComponent ApexChartRef { get; set; } = null!;

    protected DotNetObjectReference<BaseChartComponent>? _selfRef;

    private static int _id = 0;

    protected readonly string ContainerId;

    protected int ActiveTabIndex { get; set; } = 1;

    protected bool SettingsAccordionActive { get; set; } = true;

    protected EditContext EditContext { get; set; } = null!;

    protected ValidationMessageStore MessageStore { get; set; } = null!;

    protected IRgManager Manager => EntityParameters.Manager!;

    protected RgfChartParameters ChartParameters => EntityParameters.ChartParameters;

    protected ApexChartSettings ApexChartSettings { get; set; } = new();

    protected override void OnInitialized()
    {
        EntityParameters.ChartParameters.EventDispatcher.Subscribe(RgfChartEventKind.ShowChart, (arg) => OnInitSize(true));

        var aggregationSettings = ChartParameters.AggregationSettings;
        aggregationSettings.Columns = new List<RgfAggregationColumn>
        {
            new() { PropertyId = 0, Aggregate = "Count" }
        };

        EditContext = new(aggregationSettings);
        EditContext.OnValidationRequested += HandleValidationRequested;
        MessageStore = new(EditContext);

        ApexChartSettings.Options = new()
        {
            Theme = new Theme { Mode = Mode.Light, Palette = PaletteType.Palette1 },
            Chart = new()
            {
                Stacked = ChartParameters.Stacked,
                Toolbar = new() { Show = true }
            },
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

    protected virtual async Task OnInitSize(bool recreate = false)
    {
        if (!recreate && _selfRef != null)
        {
            await _jsRuntime.InvokeVoidAsync($"{RgfApexChartsConfiguration.JsApexChartsNamespace}.resize", ContainerId, _selfRef);
        }
        else
        {
            bool inited = false;
            var jquiVer = await RgfBlazorConfiguration.ChkJQueryUiVer(_jsRuntime);
            if (jquiVer >= 0)
            {
                _selfRef ??= DotNetObjectReference.Create(this);
                inited = await _jsRuntime.InvokeAsync<bool>($"{RgfApexChartsConfiguration.JsApexChartsNamespace}.initialize", ContainerId, _selfRef);
            }
            if (!inited)
            {
                _selfRef = null;
                ApexChartSettings.Height = 200;
            }
        }
    }

    [JSInvokable]
    public Task OnResize(int width, int height) => Resize(width, height);

    public virtual async Task Resize(int width, int height)
    {
        ApexChartSettings.Width = width < 1 ? null : Math.Max(width, 100);
        ApexChartSettings.Height = height < 1 ? null : Math.Max(height - 30, 100);
        StateHasChanged();
        if (RgfChartRef?.IsStateValid == true && RgfChartRef.ChartData.Count > 0)
        {
            await UpdateChart();
        }
    }

    protected void HandleValidationRequested(object? sender, ValidationRequestedEventArgs e) => RgfChartRef?.Validation(MessageStore, ChartParameters);

    protected async Task OnRedraw()
    {
        SettingsAccordionActive = false;
        StateHasChanged();
        await UpdateChart();
    }

    protected virtual async Task<bool> OnOk()
    {
        if (RgfChartRef == null || !EditContext.Validate())
        {
            return false;
        }
        SettingsAccordionActive = false;
        StateHasChanged();
        _ = Task.Run(() => OnInitSize() );
        return await OnCreate();
    }

    protected async Task<bool> OnCreate()
    {
        var toast = RgfToastEvent.CreateActionEvent(RecroDict.GetRgfUiString("Request"), Manager.EntityDesc.MenuTitle, "RecroChart");
        await Manager.ToastManager.RaiseEventAsync(toast, this);
        var success = await RgfChartRef.CreateChartDataAsyc(ChartParameters.AggregationSettings);
        if (!success)
        {
            return false;
        }
        await Manager.ToastManager.RaiseEventAsync(RgfToastEvent.RecreateToastWithStatus(toast, RecroDict.GetRgfUiString("Processed"), RgfToastType.Success, delay: 2000), this);
        await UpdateChart(true);
        return true;
    }

    protected virtual async Task UpdateChart(bool recreate = false)
    {
        if (RgfChartRef?.IsStateValid != true)
        {
            await Manager.ToastManager.RaiseEventAsync(new RgfToastEvent(RecroDict.GetRgfUiString("Warning"), RecroDict.GetRgfUiString("InvalidState"), RgfToastType.Warning), this);
            return;
        }
        if (ApexChartRef != null)
        {
            RgfToastEvent toast;
            if (recreate)
            {
                toast = RgfToastEvent.CreateActionEvent(RecroDict.GetRgfUiString("Request"), Manager.EntityDesc.MenuTitle, "Render");
                await Manager.ToastManager.RaiseEventAsync(toast, this);
                await ApexChartRef.RenderChartAsync($"{Manager.EntityDesc.Title} : ", ChartParameters.AggregationSettings, RgfChartRef.DataColumns, RgfChartRef.ChartData);
            }
            else
            {
                toast = RgfToastEvent.CreateActionEvent(RecroDict.GetRgfUiString("Request"), Manager.EntityDesc.MenuTitle, RecroDict.GetRgfUiString("Redraw"));
                await Manager.ToastManager.RaiseEventAsync(toast, this);
                await ApexChartRef.UpdateChart();
            }
            await Manager.ToastManager.RaiseEventAsync(RgfToastEvent.RecreateToastWithStatus(toast, RecroDict.GetRgfUiString("Processed"), RgfToastType.Success, 2000), this);
        }
    }

    protected Task TryUpdateChart(object? args)
    {
        if (RgfChartRef?.IsStateValid == true)
        {
            return UpdateChart();
        }
        return Task.CompletedTask;
    }

    protected async Task ChangeChartType(RgfChartSeriesType seriesType)
    {
        ChartParameters.SeriesType = seriesType;
        switch (seriesType)
        {
            case RgfChartSeriesType.Bar:
                ApexChartSettings.SeriesType = SeriesType.Bar;
                break;

            case RgfChartSeriesType.Line:
                ApexChartSettings.SeriesType = SeriesType.Line;
                break;

            case RgfChartSeriesType.Pie:
                ApexChartSettings.SeriesType = SeriesType.Pie;
                break;

            case RgfChartSeriesType.Donut:
                ApexChartSettings.SeriesType = SeriesType.Donut;
                break;
        }
        if (RgfChartRef?.IsStateValid == true)
        {
            await UpdateChart(true);
        }
    }

    protected async Task ChangedLegend(bool value)
    {
        ChartParameters.Legend = value;
        ApexChartSettings.Options.Legend = !value ? default : new Legend
        {
            Formatter = @"function(seriesName, opts) { return [seriesName, ' - ', opts.w.globals.series[opts.seriesIndex].toLocaleString()] }"
        };
        if (RgfChartRef?.IsStateValid == true)
        {
            await UpdateChart();
        }
    }

    protected virtual void OnTabActivated(int index)
    {
        ActiveTabIndex = index;
    }

    protected virtual void OnSettingsAccordionToggled()
    {
        SettingsAccordionActive = !SettingsAccordionActive;
    }
}