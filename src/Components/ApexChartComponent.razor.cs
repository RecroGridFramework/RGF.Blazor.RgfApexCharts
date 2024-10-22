using ApexCharts;
using Microsoft.AspNetCore.Components;
using Recrovit.RecroGridFramework.Abstraction.Models;
using System.Data;

namespace Recrovit.RecroGridFramework.Blazor.RgfApexCharts.Components;

public partial class ApexChartComponent : ComponentBase
{
    private ApexChart<ChartSerieData> _chartRef { get; set; } = null!;

    private List<string> xData = [];
    private List<string> xAlias = [];

    public Task UpdateChart() => _chartRef.RenderAsync();

    public async Task RenderChartAsync(string title, RgfAggregationSettings aggregationSettings, List<RgfDynamicDictionary> dataColumns, IEnumerable<RgfDynamicDictionary> chartData)
    {
        var columns = new List<string>();
        ChartSettings.Series.Clear();
        ChartSettings.Title = title;

        xAlias = [];
        foreach (var id in aggregationSettings.Groups)
        {
            for (int i = 0; i < dataColumns.Count; i++)
            {
                var propertyId = dataColumns[i].GetItemData("PropertyId")?.IntValue;
                if (propertyId == id)
                {
                    var alias = dataColumns[i].Get<string>("Alias");
                    xAlias.Add(alias);
                    break;
                }
            }
        }
        xData = chartData.GroupBy(arr => string.Join(" / ", xAlias.Select(alias => arr.GetMember(alias)?.ToString() ?? ""))).Select(e => e.Key).ToList();

        for (int i = 0; i < dataColumns.Count; i++)
        {
            var acolumn = dataColumns[i];
            var name = acolumn.Get<string>("Name");
            var aggregate = acolumn.Get<string?>("Aggregate");
            if (string.IsNullOrEmpty(aggregate))
            {
                continue;
            }
            var dataAlias = acolumn.Get<string>("Alias");
            if (i > 0)
            {
                ChartSettings.Title += ", ";
            }
            if (!string.IsNullOrEmpty(name))
            {
                ChartSettings.Title += name;
            }
            if (aggregate != "Count")
            {
                name = $"{aggregate}({name})";
            }
            if (aggregationSettings.SubGroup.Count == 0)
            {
                AddSerie(chartData, dataColumns, name, dataAlias);
            }
            else
            {
                foreach (var id in aggregationSettings.SubGroup)
                {
                    for (int j = 0; j < dataColumns.Count; j++)
                    {
                        var propertyId = dataColumns[j].GetItemData("PropertyId")?.IntValue;
                        if (propertyId == id)
                        {
                            string galias = dataColumns[j].Get<string>("Alias");
                            var group = chartData.GroupBy(e => e.GetMember(galias)?.ToString() ?? "").Select(g => g.Key).ToArray();
                            foreach (var groupItem in group)
                            {
                                var data = chartData.Where(e => e.GetMember(galias)?.ToString() == groupItem).ToArray();
                                AddSerie(data, dataColumns, groupItem, dataAlias);
                            }
                            break;
                        }
                    }
                }
            }
        }
        //await _chartRef.UpdateOptionsAsync(true, true, true);
        //await _chartRef.UpdateSeriesAsync(true);
        StateHasChanged();
        await UpdateChart();
    }

    private void AddSerie(IEnumerable<RgfDynamicDictionary> chartData, List<RgfDynamicDictionary> dataColumns, string name, string dataAlias)
    {
        var serie = new ChartSerie()
        {
            Name = name,
            Data = []
        };
        foreach (var item in xData)
        {
            var data = chartData.SingleOrDefault(e => string.Join(" / ", xAlias.Select(alias => e.GetMember(alias)?.ToString() ?? "")) == item);
            var sd = new ChartSerieData()
            {
                Y = data?.GetItemData(dataAlias).TryGetDecimal(new System.Globalization.CultureInfo("en")) ?? 0
            };
            if (xAlias.Count > 1 &&
                (ChartSettings.SeriesType == SeriesType.Bar || ChartSettings.SeriesType == SeriesType.Line) &&
                data != null)
            {
                sd.X = xAlias.Select(alias => data.GetMember(alias)?.ToString() ?? "").ToArray();
            }
            else
            {
                sd.X = item;
            }
            serie.Data.Add(sd);

        }
        ChartSettings.Series.Add(serie);
    }
}