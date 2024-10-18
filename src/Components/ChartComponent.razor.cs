using ApexCharts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Recrovit.RecroGridFramework.Abstraction.Contracts.Services;
using Recrovit.RecroGridFramework.Abstraction.Models;
using Recrovit.RecroGridFramework.Client.Blazor.Components;

namespace Recrovit.RecroGridFramework.Blazor.RgfApexCharts.Components;

public partial class ChartComponent : ComponentBase
{
    [Inject]
    private IRecroDictService RecroDict { get; set; } = null!;

    private RgfChartComponent _rgfChartRef { get; set; } = null!;

    private ApexChartComponent _chartRef { get; set; } = null!;

    private EditContext _editContext = null!;

    private ValidationMessageStore _messageStore = null!;

    [SupplyParameterFromForm]
    private RgfAggregationSettings AggregationSettings { get; set; } = new();

    public Dictionary<int, string>? ChartColumnsNumeric { get; set; }

    private ApexChartSettings _chartSettings = new();

    private bool _isCount => AggregationSettings.Columns[0].Aggregate?.Equals("Count", StringComparison.OrdinalIgnoreCase) == true;

    private bool _stacked = false;

    private bool _horizontal = false;

    private string? _title;

    protected override void OnInitialized()
    {
        AggregationSettings.Take = 100;
        _title = EntityParameters.Manager?.EntityDesc.Title;

        _editContext = new(AggregationSettings);
        _editContext.OnValidationRequested += HandleValidationRequested;
        _messageStore = new(_editContext);

        _chartSettings.Options = new()
        {
            Theme = new Theme { Mode = Mode.Light, Palette = PaletteType.Palette1 },
            Chart = new() { Stacked = _stacked },
            NoData = new NoData { Text = "No Data..." },
            PlotOptions = new PlotOptions()
            {
                Bar = new PlotOptionsBar()
                {
                    Horizontal = _horizontal,
                    DataLabels = new PlotOptionsBarDataLabels { Total = new BarTotalDataLabels { Style = new BarDataLabelsStyle { FontWeight = "800" } } }
                }
            },
            DataLabels = new DataLabels
            {
                Enabled = true,
                Formatter = "function (value) { return value.toLocaleString(); }"
            },
            Yaxis = new List<YAxis>()
            {
                new YAxis { Labels = new YAxisLabels { Formatter = "function (value) { return value.toLocaleString(); }" } }
            },
            Legend = new Legend
            {
                Formatter = @"function(seriesName, opts) { return [seriesName, ' - ', opts.w.globals.series[opts.seriesIndex].toLocaleString()] }"
            }
        };

        AggregationSettings.Columns = new List<RgfAggregationColumn>
        {
            new() { PropertyId = 0, Aggregate = "" },
            new() { PropertyId = 0, Aggregate = "Sum" },
        };
    }

    protected override void OnAfterRender(bool firstRender)
    {
        base.OnAfterRender(firstRender);
        if (firstRender)
        {
            ChartColumnsNumeric = _rgfChartRef.ChartColumns.Where(e => e.ListType == PropertyListType.Numeric || e.ClientDataType.IsNumeric()).OrderBy(e => e.ColTitle).ToDictionary(p => p.Id, p => p.ColTitle);
        }
    }

    private void HandleValidationRequested(object? sender, ValidationRequestedEventArgs e)
    {
        _messageStore.Clear();
        var prop1 = AggregationSettings.Columns[0];
        var prop2 = AggregationSettings.Columns[1];
        if (prop1.PropertyId == 0 && prop2.PropertyId == 0 && _isCount)
        {
            _messageStore.Add(() => prop1.PropertyId, "");
        }
        if (prop2.PropertyId == 0 && !_isCount ||
            prop1.PropertyId != 0 && prop1.PropertyId == prop2.PropertyId)
        {
            _messageStore.Add(() => prop2.PropertyId, "");
        }
    }

    private async Task Submit()
    {
        var res = await _rgfChartRef.CreateChartDataAsyc(AggregationSettings);
        if (res == null)
        {
            return;
        }
        StateHasChanged();
        await _chartRef.RenderAsync($"{_title} : ", AggregationSettings, _rgfChartRef.DataColumns, _rgfChartRef.ChartColumns, _rgfChartRef.ChartData);
    }

    private async Task UpdateSize()
    {
        await _chartRef.UpdateSize();
        StateHasChanged();
    }

    private async Task UpdateChart()
    {
        await _chartRef.UpdateChart();
        StateHasChanged();
    }

    private void OnChangeAggregate(string value, RgfAggregationColumn column, int idx)
    {
        column.Aggregate = value;
        _messageStore.Clear();
        if (idx == 0)
        {
            if (column.Aggregate?.Equals("Count", StringComparison.OrdinalIgnoreCase) == true)
            {
                AggregationSettings.Columns[1].Aggregate = null;
                for (int i = AggregationSettings.Columns.Count - 1; i > 1; i++)
                {
                    AggregationSettings.Columns.RemoveAt(i);
                }
            }
            else
            {
                AggregationSettings.Columns[1].Aggregate = "Sum";
            }
            AggregationSettings.Columns[1].PropertyId = 0;
        }
        StateHasChanged();
    }

    private void AddColumn(MouseEventArgs e)
    {
        AggregationSettings.Columns.Add(new RgfAggregationColumn { PropertyId = 0, Aggregate = "Sum" });
        StateHasChanged();
    }

    private void RemoveColumn(RgfAggregationColumn column)
    {
        AggregationSettings.Columns.Remove(column);
        StateHasChanged();
    }
}