using System;
using System.Diagnostics;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Win32;
using RevitHealthEnhancer.Core;
using RevitHealthEnhancer.UI;

namespace RevitHealthEnhancer.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class BimHealthImproverCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uiDocument = commandData.Application.ActiveUIDocument;

            if (uiDocument == null || uiDocument.Document == null)
            {
                message = "Open a Revit model before running Revit Model Health Enhancer.";
                return Result.Cancelled;
            }

            Document document = uiDocument.Document;
            HealthImproverOptions options = HealthImproverOptions.AllEnabled();
            IntPtr ownerHandle = Process.GetCurrentProcess().MainWindowHandle;

            if (!HealthImproverOptionsWindow.ShowDialogForOptions(options, ownerHandle))
            {
                return Result.Cancelled;
            }

            string outputPath = AskForReportPath(document);

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return Result.Cancelled;
            }

            try
            {
                HealthImproverResult result = BimHealthImprover.Run(document, outputPath, options);

                TaskDialog.Show(
                    "Revit Model Health Enhancer",
                    result.ToDialogText());

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;

                TaskDialog.Show(
                    "Revit Model Health Enhancer",
                    "The tool could not complete.\n\n" + ex.Message);

                return Result.Failed;
            }
        }

        private static string AskForReportPath(Document document)
        {
            string safeTitle = MakeSafeFileName(document.Title);

            if (string.IsNullOrWhiteSpace(safeTitle))
            {
                safeTitle = "RevitModel";
            }

            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "Save BIM Health Report",
                Filter = "HTML report (*.html)|*.html",
                FileName = safeTitle + "_BIM_Health_Report_" +
                           DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".html",
                InitialDirectory = Environment.GetFolderPath(
                    Environment.SpecialFolder.DesktopDirectory)
            };

            bool? result = dialog.ShowDialog();

            return result == true ? dialog.FileName : null;
        }

        private static string MakeSafeFileName(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                text = text.Replace(invalidChar, '_');
            }

            return text.Trim();
        }
    }
}
