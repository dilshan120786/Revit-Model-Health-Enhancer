using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;

namespace RevitHealthEnhancer.Core
{
    public sealed class BimHealthImprover
    {
        private const string TargetWorksetName = "Shared Levels and Grids";
        private const string NavisworkViewName = "Naviswork Export";

        private readonly Document document;
        private readonly HealthImproverOptions options;
        private readonly HealthImproverResult result;
        private readonly HashSet<long> deletedIds = new HashSet<long>();
        private readonly HashSet<long> updatedMarkIds = new HashSet<long>();
        private readonly Dictionary<long, int> scoreCache = new Dictionary<long, int>();
        private readonly Dictionary<int, string> worksetNameCache = new Dictionary<int, string>();

        private WorksetTable worksetTable;

        private BimHealthImprover(
            Document document,
            string outputPath,
            HealthImproverOptions options)
        {
            this.document = document;
            this.options = options ?? HealthImproverOptions.AllEnabled();
            result = new HealthImproverResult(outputPath, this.options);
        }

        public static HealthImproverResult Run(Document document, string outputPath)
        {
            return Run(document, outputPath, HealthImproverOptions.AllEnabled());
        }

        public static HealthImproverResult Run(
            Document document,
            string outputPath,
            HealthImproverOptions options)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("Report output path is required.", nameof(outputPath));
            }

            BimHealthImprover improver = new BimHealthImprover(document, outputPath, options);
            return improver.Execute();
        }

        private HealthImproverResult Execute()
        {
            List<Level> levels = Collect<Level>();
            List<Grid> grids = Collect<Grid>();
            List<RevitLinkInstance> revitLinks = Collect<RevitLinkInstance>();
            List<ImportInstance> cadLinks = Collect<ImportInstance>();
            List<Room> rooms = CollectCategory<Room>(BuiltInCategory.OST_Rooms);
            List<Space> spaces = CollectCategory<Space>(BuiltInCategory.OST_MEPSpaces);
            List<View> viewsAll = Collect<View>();
            List<ParameterFilterElement> filters = Collect<ParameterFilterElement>();
            List<TextNoteType> textTypes = Collect<TextNoteType>();
            List<TextNote> textNotes = Collect<TextNote>();

            List<View> viewTemplates = new List<View>();
            List<View> validViews = new List<View>();

            foreach (View view in viewsAll)
            {
                try
                {
                    if (!IsValidElement(view))
                    {
                        continue;
                    }

                    if (view.IsTemplate)
                    {
                        viewTemplates.Add(view);
                    }
                    else
                    {
                        validViews.Add(view);
                    }
                }
                catch
                {
                    // Some internal views may not expose every property reliably.
                }
            }

            HashSet<long> usedTemplateIds = options.DeleteUnusedViewTemplates
                ? GetUsedTemplateIds(validViews)
                : new HashSet<long>();

            HashSet<long> usedFilterIds = options.DeleteUnusedFilters
                ? GetUsedFilterIds(validViews)
                : new HashSet<long>();

            HashSet<long> usedTextTypeIds = options.DeleteUnusedTextStyles
                ? GetUsedTextTypeIds(textNotes)
                : new HashSet<long>();

            HashSet<long> taggedIds = options.DeleteDuplicateInstances
                ? GetTaggedElementIds()
                : new HashSet<long>();

            Workset targetWorkset = options.FixLevelGridWorksets
                ? GetTargetWorkset()
                : null;

            using (Transaction transaction = new Transaction(document, "BIM Health Improver"))
            {
                transaction.Start();

                if (options.PinControlElements)
                {
                    PinElements(levels, grids, revitLinks, cadLinks);
                }

                if (options.FixLevelGridWorksets)
                {
                    FixWorksets(levels, grids, targetWorkset);
                }

                if (options.DeleteInvalidRooms)
                {
                    CleanRooms(rooms);
                }

                if (options.DeleteInvalidSpaces)
                {
                    CleanSpaces(spaces);
                }

                if (options.DeleteUnusedViewTemplates)
                {
                    DeleteUnusedViewTemplates(viewTemplates, usedTemplateIds);
                }

                if (options.DeleteUnusedFilters)
                {
                    DeleteUnusedFilters(filters, usedFilterIds);
                }

                if (options.DeleteUnusedTextStyles)
                {
                    DeleteUnusedTextStyles(textTypes, usedTextTypeIds);
                }

                if (options.FixDuplicateMarks || options.DeleteDuplicateInstances)
                {
                    ProcessWarnings(taggedIds);
                }

                if (options.EnsureNavisworkView)
                {
                    CheckOrCreateNavisworkView(viewsAll);
                }

                transaction.Commit();
            }

            WriteReport();
            return result;
        }

        private List<T> Collect<T>() where T : Element
        {
            try
            {
                return new FilteredElementCollector(document)
                    .OfClass(typeof(T))
                    .Cast<T>()
                    .ToList();
            }
            catch
            {
                return new List<T>();
            }
        }

        private List<T> CollectCategory<T>(BuiltInCategory category) where T : Element
        {
            try
            {
                return new FilteredElementCollector(document)
                    .OfCategory(category)
                    .WhereElementIsNotElementType()
                    .Cast<T>()
                    .ToList();
            }
            catch
            {
                return new List<T>();
            }
        }

        private HashSet<long> GetUsedTemplateIds(IEnumerable<View> validViews)
        {
            HashSet<long> usedTemplateIds = new HashSet<long>();

            foreach (View view in validViews)
            {
                try
                {
                    if (!IsValidElement(view))
                    {
                        continue;
                    }

                    ElementId templateId = view.ViewTemplateId;

                    if (templateId != ElementId.InvalidElementId)
                    {
                        usedTemplateIds.Add(IdValue(templateId));
                    }
                }
                catch
                {
                    // Ignore views that do not expose template information.
                }
            }

            return usedTemplateIds;
        }

        private HashSet<long> GetUsedFilterIds(IEnumerable<View> validViews)
        {
            HashSet<long> usedFilterIds = new HashSet<long>();

            foreach (View view in validViews)
            {
                try
                {
                    if (!CanUseViewFilters(view))
                    {
                        continue;
                    }

                    foreach (ElementId filterId in view.GetOrderedFilters())
                    {
                        usedFilterIds.Add(IdValue(filterId));
                    }
                }
                catch
                {
                    // Not every view type supports visibility filters.
                }
            }

            return usedFilterIds;
        }

        private static HashSet<long> GetUsedTextTypeIds(IEnumerable<TextNote> textNotes)
        {
            HashSet<long> usedTextTypeIds = new HashSet<long>();

            foreach (TextNote note in textNotes)
            {
                try
                {
                    if (!IsValidElement(note))
                    {
                        continue;
                    }

                    usedTextTypeIds.Add(IdValue(note.GetTypeId()));
                }
                catch
                {
                    // Ignore invalid notes.
                }
            }

            return usedTextTypeIds;
        }

        private Workset GetTargetWorkset()
        {
            try
            {
                if (!document.IsWorkshared)
                {
                    result.Logs.Add("Workset fix skipped: model is not workshared.");
                    return null;
                }

                worksetTable = document.GetWorksetTable();

                foreach (Workset workset in new FilteredWorksetCollector(document).OfKind(WorksetKind.UserWorkset))
                {
                    if (workset.Name == TargetWorksetName)
                    {
                        return workset;
                    }
                }

                result.Logs.Add("Workset fix skipped: target workset was not found.");
            }
            catch (Exception ex)
            {
                result.Failures.Add("Workset Lookup Failure : " + ex.Message);
            }

            return null;
        }

        private string GetWorksetName(WorksetId worksetId)
        {
            int key = worksetId.IntegerValue;

            if (worksetNameCache.TryGetValue(key, out string cachedName))
            {
                return cachedName;
            }

            string name = "N/A";

            try
            {
                if (worksetTable != null)
                {
                    name = worksetTable.GetWorkset(worksetId).Name;
                }
            }
            catch
            {
                // Keep fallback name.
            }

            worksetNameCache[key] = name;
            return name;
        }

        private HashSet<long> GetTaggedElementIds()
        {
            HashSet<long> taggedIds = new HashSet<long>();
            List<IndependentTag> tags = Collect<IndependentTag>();

            MethodInfo getTaggedLocalElementIds = typeof(IndependentTag).GetMethod(
                "GetTaggedLocalElementIds",
                Type.EmptyTypes);

            PropertyInfo taggedLocalElementIdProperty = typeof(IndependentTag).GetProperty(
                "TaggedLocalElementId");

            foreach (IndependentTag tag in tags)
            {
                try
                {
                    if (!IsValidElement(tag))
                    {
                        continue;
                    }

                    if (getTaggedLocalElementIds != null)
                    {
                        object value = getTaggedLocalElementIds.Invoke(tag, null);

                        foreach (ElementId id in value as IEnumerable<ElementId> ?? new List<ElementId>())
                        {
                            if (id != null && id != ElementId.InvalidElementId)
                            {
                                taggedIds.Add(IdValue(id));
                            }
                        }
                    }
                    else if (taggedLocalElementIdProperty != null)
                    {
                        ElementId id = taggedLocalElementIdProperty.GetValue(tag, null) as ElementId;

                        if (id != null && id != ElementId.InvalidElementId)
                        {
                            taggedIds.Add(IdValue(id));
                        }
                    }
                }
                catch
                {
                    // Ignore tags that cannot resolve local element ids.
                }
            }

            return taggedIds;
        }

        private void PinElements(
            IEnumerable<Level> levels,
            IEnumerable<Grid> grids,
            IEnumerable<RevitLinkInstance> revitLinks,
            IEnumerable<ImportInstance> cadLinks)
        {
            foreach (Element element in levels.Cast<Element>()
                .Concat(grids)
                .Concat(revitLinks)
                .Concat(cadLinks))
            {
                try
                {
                    if (!IsValidElement(element))
                    {
                        continue;
                    }

                    if (!IsPinned(element))
                    {
                        element.Pinned = true;

                        result.Pinned.Add(new NameIdRecord(
                            GetName(element),
                            IdValue(element.Id)));

                        result.Logs.Add("Pinned Element : " + IdValue(element.Id));
                    }
                }
                catch (Exception ex)
                {
                    result.Failures.Add("Pin Failure : " + ex.Message);
                }
            }
        }

        private void FixWorksets(
            IEnumerable<Level> levels,
            IEnumerable<Grid> grids,
            Workset targetWorkset)
        {
            if (targetWorkset == null)
            {
                return;
            }

            int targetId = targetWorkset.Id.IntegerValue;

            foreach (Element element in levels.Cast<Element>().Concat(grids))
            {
                try
                {
                    if (!IsValidElement(element))
                    {
                        continue;
                    }

                    string currentWorksetName = GetWorksetName(element.WorksetId);

                    if (currentWorksetName == TargetWorksetName)
                    {
                        continue;
                    }

                    Parameter parameter = element.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);

                    if (parameter != null && !parameter.IsReadOnly)
                    {
                        parameter.Set(targetId);

                        result.WorksetChanges.Add(new WorksetChangeRecord(
                            GetName(element),
                            IdValue(element.Id),
                            currentWorksetName,
                            TargetWorksetName));

                        result.Logs.Add("Workset Changed : " + IdValue(element.Id));
                    }
                }
                catch (Exception ex)
                {
                    result.Failures.Add("Workset Failure : " + ex.Message);
                }
            }
        }

        private void CleanRooms(IEnumerable<Room> rooms)
        {
            foreach (Room room in rooms)
            {
                try
                {
                    if (!IsValidElement(room))
                    {
                        continue;
                    }

                    bool locationMissing;
                    bool areaInvalid;

                    try
                    {
                        locationMissing = room.Location == null;
                    }
                    catch
                    {
                        locationMissing = true;
                    }

                    try
                    {
                        areaInvalid = room.Area <= 0;
                    }
                    catch
                    {
                        areaInvalid = true;
                    }

                    if (locationMissing || areaInvalid)
                    {
                        string roomName = GetName(room);
                        long roomId = IdValue(room.Id);

                        if (DeleteElement(room.Id, "Room Delete Failure"))
                        {
                            result.RoomsDeleted.Add(new NameIdRecord(roomName, roomId));
                            result.Logs.Add("Deleted Room : " + roomId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.Failures.Add("Room Cleanup Failure : " + ex.Message);
                }
            }
        }

        private void CleanSpaces(IEnumerable<Space> spaces)
        {
            foreach (Space space in spaces)
            {
                try
                {
                    if (!IsValidElement(space))
                    {
                        continue;
                    }

                    bool locationMissing;
                    bool areaInvalid;

                    try
                    {
                        locationMissing = space.Location == null;
                    }
                    catch
                    {
                        locationMissing = true;
                    }

                    try
                    {
                        areaInvalid = space.Area <= 0;
                    }
                    catch
                    {
                        areaInvalid = true;
                    }

                    if (locationMissing || areaInvalid)
                    {
                        string spaceName = GetName(space);
                        long spaceId = IdValue(space.Id);

                        if (DeleteElement(space.Id, "Space Delete Failure"))
                        {
                            result.SpacesDeleted.Add(new NameIdRecord(spaceName, spaceId));
                            result.Logs.Add("Deleted Space : " + spaceId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.Failures.Add("Space Cleanup Failure : " + ex.Message);
                }
            }
        }

        private void DeleteUnusedViewTemplates(
            IEnumerable<View> viewTemplates,
            HashSet<long> usedTemplateIds)
        {
            foreach (View template in viewTemplates)
            {
                try
                {
                    if (!IsValidElement(template))
                    {
                        continue;
                    }

                    long templateId = IdValue(template.Id);

                    if (usedTemplateIds.Contains(templateId))
                    {
                        continue;
                    }

                    string templateName = GetName(template);

                    if (DeleteElement(template.Id, "Template Delete Failure"))
                    {
                        result.TemplatesDeleted.Add(new NameIdRecord(templateName, templateId));
                        result.Logs.Add("Deleted Template : " + templateId);
                    }
                }
                catch (Exception ex)
                {
                    result.Failures.Add("Template Failure : " + ex.Message);
                }
            }
        }

        private void DeleteUnusedFilters(
            IEnumerable<ParameterFilterElement> filters,
            HashSet<long> usedFilterIds)
        {
            foreach (ParameterFilterElement filter in filters)
            {
                try
                {
                    if (!IsValidElement(filter))
                    {
                        continue;
                    }

                    long filterId = IdValue(filter.Id);

                    if (usedFilterIds.Contains(filterId))
                    {
                        continue;
                    }

                    string filterName = GetName(filter);

                    if (DeleteElement(filter.Id, "Filter Delete Failure"))
                    {
                        result.FiltersDeleted.Add(new NameIdRecord(filterName, filterId));
                        result.Logs.Add("Deleted Filter : " + filterId);
                    }
                }
                catch (Exception ex)
                {
                    result.Failures.Add("Filter Failure : " + ex.Message);
                }
            }
        }

        private void DeleteUnusedTextStyles(
            IEnumerable<TextNoteType> textTypes,
            HashSet<long> usedTextTypeIds)
        {
            foreach (TextNoteType textType in textTypes)
            {
                try
                {
                    if (!IsValidElement(textType))
                    {
                        continue;
                    }

                    long textTypeId = IdValue(textType.Id);

                    if (usedTextTypeIds.Contains(textTypeId))
                    {
                        continue;
                    }

                    string textName = GetName(textType);

                    if (textName.Contains("<") || textName.Contains(">"))
                    {
                        continue;
                    }

                    if (DeleteElement(textType.Id, "Text Style Delete Failure"))
                    {
                        result.TextStylesDeleted.Add(new NameIdRecord(textName, textTypeId));
                        result.Logs.Add("Deleted Text Style : " + textTypeId);
                    }
                }
                catch (Exception ex)
                {
                    string idText = "Unknown";

                    try
                    {
                        idText = IdValue(textType.Id).ToString(CultureInfo.InvariantCulture);
                    }
                    catch
                    {
                        // Keep fallback id text.
                    }

                    result.Failures.Add(
                        "Text Style Failure : Element ID " + idText + " : " + ex.Message);
                }
            }
        }

        private void ProcessWarnings(HashSet<long> taggedIds)
        {
            IList<FailureMessage> warnings = document.GetWarnings();

            foreach (FailureMessage warning in warnings)
            {
                try
                {
                    string description = warning.GetDescriptionText() ?? string.Empty;
                    string lowerDescription = description.ToLowerInvariant();
                    List<long> failingIds = warning
                        .GetFailingElements()
                        .Select(IdValue)
                        .ToList();

                    if (options.FixDuplicateMarks && IsDuplicateMarkWarning(lowerDescription))
                    {
                        FixDuplicateMarks(failingIds, lowerDescription);
                    }

                    if (options.DeleteDuplicateInstances && IsDuplicateInstanceWarning(lowerDescription))
                    {
                        DeleteDuplicateInstances(failingIds, taggedIds);
                    }
                }
                catch (Exception ex)
                {
                    result.Failures.Add("Warning Automation Failure : " + ex.Message);
                }
            }
        }

        private static bool IsDuplicateMarkWarning(string description)
        {
            return description.Contains("mark")
                   && (description.Contains("duplicate") || description.Contains("same"));
        }

        private static bool IsDuplicateInstanceWarning(string description)
        {
            return description.Contains("identical instances")
                   || description.Contains("duplicate instances")
                   || description.Contains("same place");
        }

        private void FixDuplicateMarks(IEnumerable<long> failingIds, string warningDescription)
        {
            string parameterName = GetDuplicateMarkParameterName(warningDescription);
            List<Element> validElements = new List<Element>();
            HashSet<long> validElementIds = new HashSet<long>();

            foreach (long id in failingIds)
            {
                try
                {
                    if (deletedIds.Contains(id))
                    {
                        continue;
                    }

                    Element element = document.GetElement(ToElementId(id));
                    Element parameterOwner = GetDuplicateMarkParameterOwner(element, parameterName);

                    if (IsValidElement(parameterOwner))
                    {
                        long ownerId = IdValue(parameterOwner.Id);

                        if (validElementIds.Add(ownerId))
                        {
                            validElements.Add(parameterOwner);
                        }
                    }
                }
                catch
                {
                    // Ignore missing elements.
                }
            }

            if (validElements.Count < 2)
            {
                return;
            }

            validElements = validElements
                .OrderBy(element => IdValue(element.Id))
                .ToList();

            Element baseElement = null;
            string baseValue = null;

            foreach (Element element in validElements)
            {
                try
                {
                    Parameter markParameter = GetDuplicateMarkParameter(element, parameterName);

                    if (markParameter != null && markParameter.HasValue)
                    {
                        string markValue = markParameter.AsString();

                        if (!string.IsNullOrWhiteSpace(markValue))
                        {
                            baseElement = element;
                            baseValue = markValue;
                            break;
                        }
                    }
                }
                catch
                {
                    // Continue looking for a usable base mark.
                }
            }

            if (baseElement == null || string.IsNullOrWhiteSpace(baseValue))
            {
                return;
            }

            long baseId = IdValue(baseElement.Id);
            int suffixIndex = 0;

            foreach (Element element in validElements)
            {
                try
                {
                    long elementId = IdValue(element.Id);

                    if (elementId == baseId || updatedMarkIds.Contains(elementId))
                    {
                        continue;
                    }

                    Parameter markParameter = GetDuplicateMarkParameter(element, parameterName);

                    if (markParameter != null && !markParameter.IsReadOnly)
                    {
                        string newMark = baseValue + GenerateSuffix(suffixIndex);
                        markParameter.Set(newMark);
                        updatedMarkIds.Add(elementId);
                        suffixIndex++;

                        result.DuplicateMarksFixed.Add(
                            new DuplicateMarkRecord(elementId, parameterName, newMark));

                        result.Logs.Add("Duplicate " + parameterName + " Fixed : " + elementId);
                    }
                }
                catch (Exception ex)
                {
                    result.Failures.Add("Duplicate " + parameterName + " Failure : " + ex.Message);
                }
            }
        }

        private static string GetDuplicateMarkParameterName(string warningDescription)
        {
            string description = warningDescription ?? string.Empty;

            if (description.Contains("type mark") || description.Contains("type marks"))
            {
                return "Type Mark";
            }

            return "Mark";
        }

        private Element GetDuplicateMarkParameterOwner(Element element, string parameterName)
        {
            if (!IsValidElement(element))
            {
                return null;
            }

            if (parameterName != "Type Mark")
            {
                return element;
            }

            if (element is ElementType)
            {
                return element;
            }

            try
            {
                ElementId typeId = element.GetTypeId();

                if (typeId != null && typeId != ElementId.InvalidElementId)
                {
                    Element typeElement = document.GetElement(typeId);

                    if (IsValidElement(typeElement))
                    {
                        return typeElement;
                    }
                }
            }
            catch
            {
                // Fall back to the original element below.
            }

            return element;
        }

        private static Parameter GetDuplicateMarkParameter(Element element, string parameterName)
        {
            if (!IsValidElement(element))
            {
                return null;
            }

            try
            {
                Parameter parameter = parameterName == "Type Mark"
                    ? element.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_MARK)
                    : element.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);

                if (parameter != null)
                {
                    return parameter;
                }
            }
            catch
            {
                // Fall back to name lookup below.
            }

            try
            {
                return element.LookupParameter(parameterName);
            }
            catch
            {
                return null;
            }
        }

        private void DeleteDuplicateInstances(
            IEnumerable<long> failingIds,
            HashSet<long> taggedIds)
        {
            List<Element> elements = new List<Element>();

            foreach (long id in failingIds)
            {
                try
                {
                    if (deletedIds.Contains(id))
                    {
                        continue;
                    }

                    Element element = document.GetElement(ToElementId(id));

                    if (IsValidElement(element))
                    {
                        elements.Add(element);
                    }
                }
                catch
                {
                    // Ignore missing elements.
                }
            }

            if (elements.Count < 2)
            {
                return;
            }

            List<Element> validCandidates = new List<Element>();

            foreach (Element element in elements)
            {
                try
                {
                    if (IsGroupMember(element))
                    {
                        continue;
                    }

                    if (IsPinned(element))
                    {
                        continue;
                    }

                    if (IsLinkedElement(element))
                    {
                        continue;
                    }

                    validCandidates.Add(element);
                }
                catch
                {
                    // Ignore elements that cannot be safely checked.
                }
            }

            if (validCandidates.Count < 2)
            {
                return;
            }

            Element keepElement = ChooseDuplicateToKeep(validCandidates, taggedIds);
            long keepId = IdValue(keepElement.Id);

            foreach (Element element in validCandidates)
            {
                try
                {
                    long elementId = IdValue(element.Id);

                    if (elementId == keepId)
                    {
                        continue;
                    }

                    if (DeleteElement(element.Id, "Duplicate Delete Failure"))
                    {
                        result.DuplicateElementsDeleted.Add(
                            new DuplicateElementRecord(elementId, keepId));

                        result.Logs.Add("Deleted Duplicate : " + elementId);
                    }
                }
                catch (Exception ex)
                {
                    result.Failures.Add("Duplicate Delete Failure : " + ex.Message);
                }
            }
        }

        private Element ChooseDuplicateToKeep(
            List<Element> validCandidates,
            HashSet<long> taggedIds)
        {
            List<Element> tagged = validCandidates
                .Where(element => taggedIds.Contains(IdValue(element.Id)))
                .OrderBy(element => IdValue(element.Id))
                .ToList();

            if (tagged.Count > 0)
            {
                return tagged[0];
            }

            List<Element> hosted = validCandidates
                .Where(IsHosted)
                .OrderByDescending(CachedParameterScore)
                .ThenBy(element => IdValue(element.Id))
                .ToList();

            if (hosted.Count > 0)
            {
                return hosted[0];
            }

            return validCandidates
                .OrderByDescending(CachedParameterScore)
                .ThenBy(element => IdValue(element.Id))
                .First();
        }

        private void CheckOrCreateNavisworkView(IEnumerable<View> viewsAll)
        {
            bool navisExists = false;
            HashSet<string> usedViewNames = new HashSet<string>();

            foreach (View view in viewsAll)
            {
                try
                {
                    if (!IsValidElement(view))
                    {
                        continue;
                    }

                    usedViewNames.Add(view.Name);
                }
                catch
                {
                    // Ignore views without a readable name.
                }
            }

            List<View3D> threeDViews = Collect<View3D>();

            foreach (View3D view in threeDViews)
            {
                try
                {
                    if (!IsValidElement(view))
                    {
                        continue;
                    }

                    if (view.Name.ToLowerInvariant().Contains("naviswork"))
                    {
                        navisExists = true;
                        result.NavisViewStatus = "Existing Naviswork View Found : " + view.Name;
                        break;
                    }
                }
                catch
                {
                    // Ignore invalid views.
                }
            }

            if (navisExists)
            {
                return;
            }

            try
            {
                ViewFamilyType threeDType = Collect<ViewFamilyType>()
                    .FirstOrDefault(type => type.ViewFamily == ViewFamily.ThreeDimensional);

                if (threeDType == null)
                {
                    result.NavisViewStatus = "No 3D View Family Type Found";
                    return;
                }

                View3D newView = View3D.CreateIsometric(document, threeDType.Id);
                string finalViewName = NavisworkViewName;
                int nameIndex = 1;

                while (usedViewNames.Contains(finalViewName))
                {
                    finalViewName = NavisworkViewName + " " + nameIndex;
                    nameIndex++;
                }

                newView.Name = finalViewName;
                result.NavisViewStatus = "Created New 3D View : " + finalViewName;
            }
            catch (Exception ex)
            {
                result.NavisViewStatus = ex.Message;
            }
        }

        private bool DeleteElement(ElementId elementId, string failurePrefix)
        {
            long id = IdValue(elementId);

            if (deletedIds.Contains(id))
            {
                return true;
            }

            try
            {
                document.Delete(elementId);
                deletedIds.Add(id);
                return true;
            }
            catch (Exception ex)
            {
                result.Failures.Add(failurePrefix + " : " + ex.Message);
            }

            return false;
        }

        private int CachedParameterScore(Element element)
        {
            long elementId = IdValue(element.Id);

            if (!scoreCache.TryGetValue(elementId, out int score))
            {
                score = ParameterScore(element);
                scoreCache[elementId] = score;
            }

            return score;
        }

        private static int ParameterScore(Element element)
        {
            int score = 0;

            if (!IsValidElement(element))
            {
                return score;
            }

            try
            {
                foreach (Parameter parameter in element.Parameters)
                {
                    try
                    {
                        if (parameter == null || !parameter.HasValue)
                        {
                            continue;
                        }

                        StorageType storageType = parameter.StorageType;

                        if (storageType == StorageType.String)
                        {
                            if (!string.IsNullOrWhiteSpace(parameter.AsString()))
                            {
                                score++;
                            }
                        }
                        else if (storageType == StorageType.ElementId)
                        {
                            if (parameter.AsElementId() != ElementId.InvalidElementId)
                            {
                                score++;
                            }
                        }
                        else
                        {
                            score++;
                        }
                    }
                    catch
                    {
                        // Ignore parameters that cannot be read.
                    }
                }
            }
            catch
            {
                // Ignore elements that do not expose parameters reliably.
            }

            return score;
        }

        private static bool IsValidElement(Element element)
        {
            try
            {
                return element != null && element.IsValidObject;
            }
            catch
            {
                return false;
            }
        }

        private static bool CanUseViewFilters(View view)
        {
            try
            {
                return IsValidElement(view) && view.AreGraphicsOverridesAllowed();
            }
            catch
            {
                return false;
            }
        }

        private static bool IsPinned(Element element)
        {
            try
            {
                return element.Pinned;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsHosted(Element element)
        {
            try
            {
                PropertyInfo hostProperty = element.GetType().GetProperty("Host");

                if (hostProperty == null)
                {
                    return false;
                }

                object host = hostProperty.GetValue(element, null);
                return host != null;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsGroupMember(Element element)
        {
            try
            {
                return element.GroupId != ElementId.InvalidElementId;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsLinkedElement(Element element)
        {
            return element is RevitLinkInstance;
        }

        private static string GetName(Element element)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(element.Name))
                {
                    return element.Name;
                }
            }
            catch
            {
                // Try parameter fallback.
            }

            try
            {
                Parameter parameter = element.LookupParameter("Name");

                if (parameter != null)
                {
                    string value = parameter.AsString();

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }
            catch
            {
                // Keep fallback name.
            }

            return "N/A";
        }

        private static string GenerateSuffix(int index)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            int value = index;
            string suffix = string.Empty;

            while (true)
            {
                int remainder = value % 26;
                suffix = alphabet[remainder] + suffix;
                value = value / 26;

                if (value == 0)
                {
                    break;
                }

                value--;
            }

            return "_" + suffix;
        }

        private static long IdValue(ElementId id)
        {
#if REVIT2024_OR_GREATER
            return id == null ? 0 : id.Value;
#else
#pragma warning disable CS0618
            return id == null ? 0 : id.IntegerValue;
#pragma warning restore CS0618
#endif
        }

        private static ElementId ToElementId(long id)
        {
#if REVIT2024_OR_GREATER
            return new ElementId(id);
#else
            return new ElementId((int)id);
#endif
        }

        private void WriteReport()
        {
            string directory = Path.GetDirectoryName(result.OutputPath);

            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(result.OutputPath, BuildHtmlReport(result), Encoding.UTF8);
        }

        private static string BuildHtmlReport(HealthImproverResult report)
        {
            StringBuilder html = new StringBuilder();

            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset=\"utf-8\">");
            html.AppendLine("<style>");
            html.AppendLine("body { font-family: Arial, sans-serif; background: #f4f6f8; color: #1f2933; }");
            html.AppendLine("h1 { text-align: center; color: #2c3e50; }");
            html.AppendLine(".card { background: white; margin: 15px; padding: 15px; border-radius: 8px; box-shadow: 0 2px 8px rgba(0,0,0,0.1); }");
            html.AppendLine("table { width: 100%; border-collapse: collapse; }");
            html.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; vertical-align: top; }");
            html.AppendLine("th { background: #3498db; color: white; }");
            html.AppendLine(".section-title { color: #2c3e50; }");
            html.AppendLine("</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine("<h1>BIM MODEL HEALTH IMPROVER REPORT</h1>");

            html.AppendLine("<div class=\"card\">");
            html.AppendLine("<h2 class=\"section-title\">SUMMARY</h2>");
            html.AppendLine("<p><b>Mode :</b> Actual Changes Applied</p>");
            AppendSummaryLine(html, "Selected Actions", report.SelectedActions.Count);
            AppendSummaryLine(html, "Pinned Elements", report.Pinned.Count);
            AppendSummaryLine(html, "Workset Changes", report.WorksetChanges.Count);
            AppendSummaryLine(html, "Rooms Deleted", report.RoomsDeleted.Count);
            AppendSummaryLine(html, "Spaces Deleted", report.SpacesDeleted.Count);
            AppendSummaryLine(html, "Unused Templates Deleted", report.TemplatesDeleted.Count);
            AppendSummaryLine(html, "Unused Filters Deleted", report.FiltersDeleted.Count);
            AppendSummaryLine(html, "Unused Text Styles Deleted", report.TextStylesDeleted.Count);
            AppendSummaryLine(html, "Duplicate Marks Fixed", report.DuplicateMarksFixed.Count);
            AppendSummaryLine(html, "Duplicate Elements Deleted", report.DuplicateElementsDeleted.Count);
            AppendSummaryLine(html, "Total Failures", report.Failures.Count);
            html.AppendLine("<p><b>Naviswork View Status :</b> " + Escape(report.NavisViewStatus) + "</p>");
            html.AppendLine("</div>");

            AppendList(html, "Selected Actions", report.SelectedActions);
            AppendDuplicateMarkTable(html, report.DuplicateMarksFixed);
            AppendDuplicateElementTable(html, report.DuplicateElementsDeleted);
            AppendWorksetTable(html, report.WorksetChanges);
            AppendNameIdTable(html, "Pinned Elements", report.Pinned);
            AppendNameIdTable(html, "Rooms Deleted", report.RoomsDeleted);
            AppendNameIdTable(html, "Spaces Deleted", report.SpacesDeleted);
            AppendNameIdTable(html, "Unused Templates Deleted", report.TemplatesDeleted);
            AppendNameIdTable(html, "Unused Filters Deleted", report.FiltersDeleted);
            AppendNameIdTable(html, "Unused Text Styles Deleted", report.TextStylesDeleted);
            AppendList(html, "Failures", report.Failures);
            AppendList(html, "Logs", report.Logs);

            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
        }

        private static void AppendSummaryLine(StringBuilder html, string label, int count)
        {
            html.AppendLine(
                "<p><b>" + Escape(label) + " :</b> " +
                count.ToString(CultureInfo.InvariantCulture) + "</p>");
        }

        private static void AppendNameIdTable(
            StringBuilder html,
            string title,
            IList<NameIdRecord> records)
        {
            html.AppendLine("<div class=\"card\">");
            html.AppendLine("<h2 class=\"section-title\">" + Escape(title) + "</h2>");
            html.AppendLine("<table>");
            html.AppendLine("<tr><th>Name</th><th>ID</th></tr>");

            if (records.Count == 0)
            {
                html.AppendLine("<tr><td colspan=\"2\">None</td></tr>");
            }
            else
            {
                foreach (NameIdRecord record in records)
                {
                    html.AppendLine(
                        "<tr><td>" + Escape(record.Name) + "</td><td>" +
                        record.Id.ToString(CultureInfo.InvariantCulture) + "</td></tr>");
                }
            }

            html.AppendLine("</table>");
            html.AppendLine("</div>");
        }

        private static void AppendWorksetTable(
            StringBuilder html,
            IList<WorksetChangeRecord> records)
        {
            html.AppendLine("<div class=\"card\">");
            html.AppendLine("<h2 class=\"section-title\">Workset Changes</h2>");
            html.AppendLine("<table>");
            html.AppendLine("<tr><th>Name</th><th>ID</th><th>From</th><th>To</th></tr>");

            if (records.Count == 0)
            {
                html.AppendLine("<tr><td colspan=\"4\">None</td></tr>");
            }
            else
            {
                foreach (WorksetChangeRecord record in records)
                {
                    html.AppendLine(
                        "<tr><td>" + Escape(record.Name) + "</td><td>" +
                        record.Id.ToString(CultureInfo.InvariantCulture) + "</td><td>" +
                        Escape(record.From) + "</td><td>" +
                        Escape(record.To) + "</td></tr>");
                }
            }

            html.AppendLine("</table>");
            html.AppendLine("</div>");
        }

        private static void AppendDuplicateMarkTable(
            StringBuilder html,
            IList<DuplicateMarkRecord> records)
        {
            html.AppendLine("<div class=\"card\">");
            html.AppendLine("<h2 class=\"section-title\">Duplicate Mark Warnings Fixed</h2>");
            html.AppendLine("<table>");
            html.AppendLine("<tr><th>Element ID</th><th>Parameter</th><th>New Value</th></tr>");

            if (records.Count == 0)
            {
                html.AppendLine("<tr><td colspan=\"3\">None</td></tr>");
            }
            else
            {
                foreach (DuplicateMarkRecord record in records)
                {
                    html.AppendLine(
                        "<tr><td>" + record.ElementId.ToString(CultureInfo.InvariantCulture) +
                        "</td><td>" + Escape(record.ParameterName) +
                        "</td><td>" + Escape(record.NewMark) + "</td></tr>");
                }
            }

            html.AppendLine("</table>");
            html.AppendLine("</div>");
        }

        private static void AppendDuplicateElementTable(
            StringBuilder html,
            IList<DuplicateElementRecord> records)
        {
            html.AppendLine("<div class=\"card\">");
            html.AppendLine("<h2 class=\"section-title\">Duplicate Elements Deleted</h2>");
            html.AppendLine("<table>");
            html.AppendLine("<tr><th>Deleted Element</th><th>Kept Element</th></tr>");

            if (records.Count == 0)
            {
                html.AppendLine("<tr><td colspan=\"2\">None</td></tr>");
            }
            else
            {
                foreach (DuplicateElementRecord record in records)
                {
                    html.AppendLine(
                        "<tr><td>" + record.DeletedElementId.ToString(CultureInfo.InvariantCulture) +
                        "</td><td>" + record.KeptElementId.ToString(CultureInfo.InvariantCulture) +
                        "</td></tr>");
                }
            }

            html.AppendLine("</table>");
            html.AppendLine("</div>");
        }

        private static void AppendList(
            StringBuilder html,
            string title,
            IList<string> items)
        {
            html.AppendLine("<div class=\"card\">");
            html.AppendLine("<h2 class=\"section-title\">" + Escape(title) + "</h2>");
            html.AppendLine("<ul>");

            if (items.Count == 0)
            {
                html.AppendLine("<li>None</li>");
            }
            else
            {
                foreach (string item in items)
                {
                    html.AppendLine("<li>" + Escape(item) + "</li>");
                }
            }

            html.AppendLine("</ul>");
            html.AppendLine("</div>");
        }

        private static string Escape(string value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }
    }

    public sealed class HealthImproverResult
    {
        public HealthImproverResult(string outputPath, HealthImproverOptions options)
        {
            OutputPath = outputPath;
            NavisViewStatus = string.Empty;
            SelectedActions = new List<string>(
                (options ?? HealthImproverOptions.AllEnabled()).GetSelectedActionNames());
            Pinned = new List<NameIdRecord>();
            WorksetChanges = new List<WorksetChangeRecord>();
            RoomsDeleted = new List<NameIdRecord>();
            SpacesDeleted = new List<NameIdRecord>();
            TemplatesDeleted = new List<NameIdRecord>();
            FiltersDeleted = new List<NameIdRecord>();
            TextStylesDeleted = new List<NameIdRecord>();
            DuplicateMarksFixed = new List<DuplicateMarkRecord>();
            DuplicateElementsDeleted = new List<DuplicateElementRecord>();
            Failures = new List<string>();
            Logs = new List<string>();
        }

        public string OutputPath { get; private set; }
        public string NavisViewStatus { get; set; }
        public IList<string> SelectedActions { get; private set; }
        public IList<NameIdRecord> Pinned { get; private set; }
        public IList<WorksetChangeRecord> WorksetChanges { get; private set; }
        public IList<NameIdRecord> RoomsDeleted { get; private set; }
        public IList<NameIdRecord> SpacesDeleted { get; private set; }
        public IList<NameIdRecord> TemplatesDeleted { get; private set; }
        public IList<NameIdRecord> FiltersDeleted { get; private set; }
        public IList<NameIdRecord> TextStylesDeleted { get; private set; }
        public IList<DuplicateMarkRecord> DuplicateMarksFixed { get; private set; }
        public IList<DuplicateElementRecord> DuplicateElementsDeleted { get; private set; }
        public IList<string> Failures { get; private set; }
        public IList<string> Logs { get; private set; }

        public string ToDialogText()
        {
            StringBuilder builder = new StringBuilder();

            builder.AppendLine("BIM Health Improver completed.");
            builder.AppendLine();
            builder.AppendLine("Actual changes were applied.");
            builder.AppendLine("Selected Actions: " + SelectedActions.Count);
            builder.AppendLine("Pinned Elements: " + Pinned.Count);
            builder.AppendLine("Workset Changes: " + WorksetChanges.Count);
            builder.AppendLine("Rooms Deleted: " + RoomsDeleted.Count);
            builder.AppendLine("Spaces Deleted: " + SpacesDeleted.Count);
            builder.AppendLine("Unused Templates Deleted: " + TemplatesDeleted.Count);
            builder.AppendLine("Unused Filters Deleted: " + FiltersDeleted.Count);
            builder.AppendLine("Unused Text Styles Deleted: " + TextStylesDeleted.Count);
            builder.AppendLine("Duplicate Marks Fixed: " + DuplicateMarksFixed.Count);
            builder.AppendLine("Duplicate Elements Deleted: " + DuplicateElementsDeleted.Count);
            builder.AppendLine("Failures: " + Failures.Count);
            builder.AppendLine("Naviswork View: " + NavisViewStatus);
            builder.AppendLine();
            builder.AppendLine("HTML Report:");
            builder.AppendLine(OutputPath);

            return builder.ToString();
        }
    }

    public sealed class NameIdRecord
    {
        public NameIdRecord(string name, long id)
        {
            Name = name;
            Id = id;
        }

        public string Name { get; private set; }
        public long Id { get; private set; }
    }

    public sealed class WorksetChangeRecord
    {
        public WorksetChangeRecord(string name, long id, string from, string to)
        {
            Name = name;
            Id = id;
            From = from;
            To = to;
        }

        public string Name { get; private set; }
        public long Id { get; private set; }
        public string From { get; private set; }
        public string To { get; private set; }
    }

    public sealed class DuplicateMarkRecord
    {
        public DuplicateMarkRecord(long elementId, string parameterName, string newMark)
        {
            ElementId = elementId;
            ParameterName = parameterName;
            NewMark = newMark;
        }

        public long ElementId { get; private set; }
        public string ParameterName { get; private set; }
        public string NewMark { get; private set; }
    }

    public sealed class DuplicateElementRecord
    {
        public DuplicateElementRecord(long deletedElementId, long keptElementId)
        {
            DeletedElementId = deletedElementId;
            KeptElementId = keptElementId;
        }

        public long DeletedElementId { get; private set; }
        public long KeptElementId { get; private set; }
    }
}

