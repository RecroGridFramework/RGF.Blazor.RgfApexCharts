/*!
* recrovit-rgf-apexcharts.js v1.1.0
*/

window.Recrovit = window.Recrovit || {};
window.Recrovit.RGF = window.Recrovit.RGF || {};
window.Recrovit.RGF.Blazor = window.Recrovit.RGF.Blazor || {};
var Blazor = window.Recrovit.RGF.Blazor;

Blazor.ApexCharts = {
    initialize: async function (containerId, chartRef) {
        var container = $(`#${containerId}`);
        var dialog = container.parents('div.modal-content').first();
        if (dialog.resizable('instance') == null) {
            return false;
        }
        dialog.on('resizestop', function (event, ui) {
            RgfApexCharts.resize(containerId, chartRef);
        });
        await RgfApexCharts.resize(containerId, chartRef);
        return true;
    },
    resize: async function (containerId, chartRef) {
        var container = $(`#${containerId}`).parent();
        var w = Math.floor($('.rgf-apexchart-content', container).first().width());
        var h1 = Math.floor(container.height());
        var h = h1 - Math.floor($('.rgf-apexchart-header', container).first().height());
        await chartRef.invokeMethodAsync('OnResize', w, h);
    }
};

var RgfApexCharts = Blazor.ApexCharts;