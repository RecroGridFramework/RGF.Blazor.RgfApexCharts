using ApexCharts;
using Microsoft.AspNetCore.Components;
using Recrovit.RecroGridFramework.Abstraction.Models;
using System.Text.RegularExpressions;

namespace Recrovit.RecroGridFramework.Blazor.RgfApexCharts.Components;

public partial class ApexChartComponent : ComponentBase
{
    private ApexChart<ChartSerieData> _chartRef { get; set; } = null!;

    public async Task RenderAsync(string title, RgfAggregationSettings aggregationSettings, string[] dataColumns, IRgfProperty[] chartColumns, List<RgfDynamicDictionary> chartData)
    {
        ChartSettings.Series.Clear();
        ChartSettings.Title = title;

        var prop1 = aggregationSettings.Columns[0];
        var prop2 = aggregationSettings.Columns[1];
        bool isCount = aggregationSettings.Columns[0].Aggregate?.Equals("Count", StringComparison.OrdinalIgnoreCase) == true;

        for (int i = 0; i < aggregationSettings.Columns.Count; i++)
        {
            var colTitle = chartColumns.SingleOrDefault(e => e.Id == aggregationSettings.Columns[i].PropertyId)?.ColTitle;
            if (i == 1)
            {
                ChartSettings.Title += " / ";
            }
            if (i > 1)
            {
                ChartSettings.Title += ", ";
            }
            if (!string.IsNullOrEmpty(colTitle))
            {
                ChartSettings.Title += i == 0 || isCount ? colTitle : $"{aggregationSettings.Columns[i].Aggregate}({colTitle})";
            }
            else if (isCount)
            {
                ChartSettings.Title += "Count";
            }
        }

        int yProp = 0;
        int xProp = 1;
        if (isCount)
        {
            if (dataColumns.Length > 2)
            {
                var group1arr = chartData.GroupBy(e => e.GetItemData(dataColumns[1]).StringValue).Select(g => new { Group = g.Key })
                    .OrderBy(e => e.Group)
                    .ToArray();

                var group2arr = chartData.GroupBy(e => e.GetItemData(dataColumns[2]).StringValue).Select(g => new { Group = g.Key })
                    .OrderBy(e => e.Group)
                    .ToArray();

                foreach (var group2 in group2arr)
                {
                    var g2data = chartData.Where(e => group2.Group == e.GetItemData(dataColumns[2]).StringValue).ToArray();
                    var serie = new ChartSerie();
                    var prop = chartColumns.SingleOrDefault(e => e.Alias.Equals(group2.Group, StringComparison.OrdinalIgnoreCase));
                    if (prop == null)
                    {
                        prop = chartColumns.SingleOrDefault(e => e.Alias.Equals(Regex.Replace(group2.Group, @"\d+$", ""), StringComparison.OrdinalIgnoreCase));
                    }
                    serie.Name = prop?.ColTitle ?? group2.Group;
                    foreach (var group1 in group1arr)
                    {
                        var cd = new ChartSerieData { X = group1.Group, Y = 0 };
                        var data = g2data.SingleOrDefault(e => group1.Group == e.GetItemData(dataColumns[1]).StringValue);
                        if (data != null)
                        {
                            cd.Y = data.GetItemData(dataColumns[yProp]).TryGetDecimal(new System.Globalization.CultureInfo("en")) ?? 0;
                        }
                        serie.Data.Add(cd);
                    }
                    ChartSettings.Series.Add(serie);
                }
            }
            else
            {
                var chart = new ChartSerie();
                chart.Name = "Count";
                chart.Data = chartData.Select(e => new ChartSerieData
                {
                    X = e.GetItemData(dataColumns[xProp]).StringValue,
                    Y = e.GetItemData(dataColumns[yProp]).TryGetDecimal(new System.Globalization.CultureInfo("en")) ?? 0
                }).OrderBy(e => e.X).ToList();
                ChartSettings.Series.Add(chart);
            }
        }
        else
        {
            var g1 = chartColumns.SingleOrDefault(e => e.Id == prop1.PropertyId)?.Alias;
            if (g1 != null)
            {
                xProp = dataColumns.ToList().FindIndex(e => string.Equals(e, g1, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                xProp = 0;
            }
            for (yProp = 0; yProp < dataColumns.Length; yProp++)
            {
                if (yProp == xProp && g1 != null)
                {
                    continue;
                }
                var serie = new ChartSerie();
                var prop = chartColumns.SingleOrDefault(e => e.Alias.Equals(dataColumns[yProp], StringComparison.OrdinalIgnoreCase));
                if (prop == null)
                {
                    prop = chartColumns.SingleOrDefault(e => e.Alias.Equals(Regex.Replace(dataColumns[yProp], @"\d+$", ""), StringComparison.OrdinalIgnoreCase));
                }
                serie.Name = prop?.ColTitle ?? dataColumns[yProp];
                serie.Data = chartData.Select(e => new ChartSerieData
                {
                    X = g1 == null ? serie.Name : e.GetItemData(dataColumns[xProp]).StringValue,
                    Y = e.GetItemData(dataColumns[yProp]).TryGetDecimal(new System.Globalization.CultureInfo("en")) ?? 0
                }).OrderBy(e => e.X).ToList();
                ChartSettings.Series.Add(serie);
            }
        }
        StateHasChanged();
        await _chartRef.UpdateSeriesAsync(true);
        await _chartRef.UpdateOptionsAsync(true, true, true);
    }

    public async Task UpdateSize()
    {
        await _chartRef.UpdateOptionsAsync(true, true, false);
    }

    public async Task UpdateChart()
    {
        await _chartRef.RenderAsync();
        StateHasChanged();
    }
}