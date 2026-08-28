using System;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.UI;
using RevitHealthEnhancer.Commands;
using RevitHealthEnhancer.UI;

namespace RevitHealthEnhancer
{
    public class App : IExternalApplication
    {
        private const string TabName = "Model Health";
        private const string HealthPanelName = "Health Enhancer";
        private const string HelpPanelName = "About";

        public Result OnStartup(UIControlledApplication application)
        {
            CreateRibbonTab(application);

            RibbonPanel healthPanel = GetOrCreatePanel(application, TabName, HealthPanelName);
            RibbonPanel helpPanel = GetOrCreatePanel(application, TabName, HelpPanelName);

            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            AddHealthImproverButton(healthPanel, assemblyPath);
            AddAboutButton(helpPanel, assemblyPath);

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        private static void CreateRibbonTab(UIControlledApplication application)
        {
            try
            {
                application.CreateRibbonTab(TabName);
            }
            catch
            {
                // Revit throws if the tab already exists.
            }
        }

        private static RibbonPanel GetOrCreatePanel(
            UIControlledApplication application,
            string tabName,
            string panelName)
        {
            RibbonPanel existingPanel = application
                .GetRibbonPanels(tabName)
                .FirstOrDefault(panel => panel.Name == panelName);

            return existingPanel ?? application.CreateRibbonPanel(tabName, panelName);
        }

        private static void AddHealthImproverButton(RibbonPanel panel, string assemblyPath)
        {
            if (PanelContainsItem(panel, "BimHealthImprover"))
            {
                return;
            }

            PushButtonData buttonData = new PushButtonData(
                "BimHealthImprover",
                "Health\nEnhancer",
                assemblyPath,
                typeof(BimHealthImproverCommand).FullName);

            buttonData.ToolTip = "Run BIM model health cleanup and create an HTML report.";
            buttonData.LongDescription =
                "Pins levels, grids, Revit links and CAD links; moves levels and grids to the configured workset; " +
                "removes invalid rooms and spaces; removes unused view templates, filters and text styles; " +
                "fixes duplicate Mark warnings; removes safe duplicate instances; and creates a Naviswork export 3D view.";
            buttonData.Image = RibbonIcons.CreateHealthIcon();
            buttonData.LargeImage = RibbonIcons.CreateHealthIcon();

            panel.AddItem(buttonData);
        }

        private static void AddAboutButton(RibbonPanel panel, string assemblyPath)
        {
            if (PanelContainsItem(panel, "AboutRevitHealthEnhancer"))
            {
                return;
            }

            PushButtonData buttonData = new PushButtonData(
                "AboutRevitHealthEnhancer",
                "About",
                assemblyPath,
                typeof(AboutCommand).FullName);

            buttonData.ToolTip = "Show information about the Revit Model Health Enhancer.";
            buttonData.Image = RibbonIcons.CreateInfoIcon();
            buttonData.LargeImage = RibbonIcons.CreateInfoIcon();

            panel.AddItem(buttonData);
        }

        private static bool PanelContainsItem(RibbonPanel panel, string itemName)
        {
            return panel.GetItems().Any(item => item.Name == itemName);
        }
    }
}
