using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitHealthEnhancer.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class AboutCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            TaskDialog.Show(
                "Revit Model Health Enhancer",
                "Revit Model Health Enhancer\n" +
                "Version 1.0.0\n\n" +
                "Developed by Dilshan Khan\n\n" +
                "Automates BIM model health checks, element pinning, workset management, " +
                "unused asset cleanup, warning resolution, and HTML audit reporting.");

            return Result.Succeeded;
        }
    }
}
