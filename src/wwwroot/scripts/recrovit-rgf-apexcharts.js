/*!
* recrovit-rgf-apexcharts.js v1.0.0
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
            RgfApexCharts.onResize($(`#${containerId}`).parent(), chartRef);
        });
        window.setTimeout(function () {
            RgfApexCharts.onResize(container.parent(), chartRef);
        }, 10);
        return true;
    },
    onResize: async function (element, chartRef) {
        var container = $(element);
        var w = Math.round(container.width());
        var h = Math.round(container.height());
        await chartRef.invokeMethodAsync('Resize', w - 1, h - 16);
    }
};

var RgfApexCharts = Blazor.ApexCharts;