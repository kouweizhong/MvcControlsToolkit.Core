﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System.Reflection;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Html;
using MvcControlsToolkit.Core.Views;
using MvcControlsToolkit.Core.TagHelpers;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Globalization;
using System.Security.Principal;
using Microsoft.Extensions.Localization;
using System.Collections;
using Microsoft.AspNetCore.Http;
using MvcControlsToolkit.Core.Filters;

namespace MvcControlsToolkit.Core.Templates
{
    public class RowType
    {
        protected static IDictionary<Type, string> conventionKeys = new ConcurrentDictionary<Type, string>();
        protected static IDictionary<Type, IEnumerable<Column>> allColumns = new ConcurrentDictionary<Type, IEnumerable<Column>>();
        protected static ConcurrentDictionary<string , IList<RowType>> rowsCollections = new ConcurrentDictionary<string, IList<RowType>>();
        public Template<RowType> EditTemplate { get; set; }
        public Template<RowType> DisplayTemplate { get; set; }
        public IEnumerable<Column> Columns { get; private set; }
        public string KeyName { get; private set; }
        public Type ControllerType { get; set; }
        public string RowTitle { get; set; }
        public ModelExpression For { get; private set; }
        public bool IsDetail { get; private set; }
        public bool CustomButtons { get; set; }
        public string RowCssClass { get; set; }
        public string InputCssClass { get; set; }
        public string CheckboxCssClass { get; set; }
        public Type LocalizationType { get; set; }
        public uint Order { get; set; }
        private TypeInfo _TypeInfos = null;
        private int _ColumnsCount=-1;
        public int ColumnsCount
        {
            get
            {
                if (_ColumnsCount < 0) _ColumnsCount = columnsCount();
                return _ColumnsCount;
            }
        }
        public TypeInfo TypeInfos
        {
            get
            {
                if (_TypeInfos == null) _TypeInfos = For.Metadata.ModelType.GetTypeInfo();
                return _TypeInfos;
            }
        }
        public Func<IPrincipal,Functionalities> RequiredFunctionalities { get; set;}
        protected bool columnsPrepared;
        protected Func<IEnumerable<Column>, ContextualizedHelpers, object, IHtmlContent> renderHiddens;
        protected IList<Column> hiddens;
        protected Column keyColumn;
        public static IList<RowType>  GetRowsCollection(string key)
        {
            IList<RowType> rowList;
            if (rowsCollections.TryGetValue(key, out rowList)) return rowList;
            else return null;
        }
        public static void CacheRowGroup(string key, IList<RowType> rowsCollection, HttpContext ctx)
        {
            Action action = () =>
            {
                rowsCollections.TryAdd(key, rowsCollection);
            };
            CacheViewPartsFilter.AddAction(ctx, action);
        }
        
        protected static string GetConventionKey(Type t)
        {
            string res;
            if (conventionKeys.TryGetValue(t, out res)) return res;
            string byName = null;
            foreach (var prop in t.GetTypeInfo().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty))
            {
                if (prop.Name.ToLowerInvariant() == "id")
                {
                    byName = prop.Name;
                    continue;
                }
                var att = prop.GetCustomAttribute(typeof(KeyAttribute), false);
                if (att != null) return prop.Name;
            }
            return byName;
        }
        private int columnsCount()
        {
            int res = 0;
            foreach (var col in Columns)
            {
                if (col.ColSpan.HasValue) res += col.ColSpan.Value;
                else res++;
            }
            return res;
        }
        private IEnumerable<Column> visibleAndHiddenColumns = null;
        protected virtual void PrepareColumns()
        {
            if (columnsPrepared) return;
            int i = 0;
            var newCols = new List<Column>();
            bool keyFound = false;
            foreach (var col in Columns)
            {
                col.Row = this;
                col.IsDetail = IsDetail;
                col.Prepare();
                col.NaturalOrder = i;
                i++;
                if (col.For.Metadata.PropertyName == KeyName)
                {
                    keyFound = true;
                    keyColumn = col;
                    if (!keyColumn.Hidden.HasValue) keyColumn.Hidden = true;
                }
                if (col.Hidden.Value)
                {
                    if (hiddens == null) hiddens = new List<Column>();
                    hiddens.Add(col);
                }
                    
                else
                {
                    newCols.Add(col);
                    if (col.InputCssClass == null) col.InputCssClass = this.InputCssClass;
                    if (col.CheckboxCssClass == null) col.CheckboxCssClass = this.CheckboxCssClass;
                }
                

            }
            if (!keyFound)
            {
                var prop = For.ModelExplorer.GetExplorerForProperty(KeyName);
                keyColumn = new Column(new ModelExpression(prop.Metadata.PropertyName, prop), null, isDetail: IsDetail);
                if(!keyColumn.Hidden.HasValue) keyColumn.Hidden = true;
                if (hiddens == null) hiddens = new List<Column>();
                hiddens.Add(keyColumn);
            }
            visibleAndHiddenColumns = Columns;
            Columns = newCols.OrderByDescending(m => m.Order).ThenBy(m => m.NaturalOrder);
            
            columnsPrepared = true;
        }
        protected virtual IEnumerable<Column> StandardColumns(bool isDetail)
        {
            IEnumerable<Column> res;
            if (allColumns.TryGetValue(For.Metadata.ModelType, out res)) return res;
            var pres = new List<Column>();
            foreach (var prop in For.ModelExplorer.Properties)
            {
                if (prop.Metadata.IsComplexType
                    ||
                    (!(prop.Metadata.ModelType == typeof(string)) && prop.Metadata.IsEnumerableType)) continue;
                var col = new Column(new ModelExpression(prop.Metadata.PropertyName, prop), null, isDetail: isDetail);
                if (col.For.Metadata.PropertyName == KeyName) col.Hidden = true;
                pres.Add(col);
            }
            res = pres;
            allColumns[For.Metadata.ModelType] = res;
            return res;
        }
        protected virtual void InheritColumns(
            IEnumerable<Column> inherited,
            IEnumerable<Column> addColumns = null,
            IEnumerable<ModelExpression> removeColumns = null)
        {
            var standardColumns = inherited;
            if (removeColumns != null || addColumns != null)
            {
                IEnumerable<string> toRemove;
                if (removeColumns != null)
                {
                    toRemove = removeColumns.Select(m => m.Metadata.PropertyName);
                    if (addColumns != null) toRemove = toRemove.Union(addColumns.Select(m => m.For.Metadata.PropertyName));
                }
                else toRemove = addColumns.Select(m => m.For.Metadata.PropertyName);
                var set = new HashSet<string>(toRemove);
                standardColumns = standardColumns.Where(m => !set.Contains(m.For.Metadata.PropertyName));
            }
            if (addColumns != null) standardColumns = standardColumns.Union(addColumns);
            Columns = standardColumns;
            PrepareColumns();
        }
        internal void RowInit(IList<RowType> x)
        {
            if (init != null) init(x);
        }
        private Action<IList<RowType>> init=null;
        public RowType(ModelExpression expression,
            uint inheritIndex,
            bool isDetail = false,
            IEnumerable<Column> addColumns = null,
            IEnumerable<ModelExpression> removeColumns = null,
            Func<IEnumerable<Column>, ContextualizedHelpers, object, IHtmlContent> renderHiddens = null)
        {
            IsDetail = isDetail;
            For = expression;
            init = x =>
             {
                 RowType inheritFrom = null;
                 string keyName = null;
                 if(x != null && inheritIndex <x.Count && inheritIndex>=0)
                 {
                     inheritFrom = x[(int)inheritIndex];
                     keyName=inheritFrom.KeyName;
                 }
                 
                 this.renderHiddens = renderHiddens;
                 if (inheritFrom == null || inheritFrom.Columns == null) throw new ArgumentNullException(nameof(inheritFrom));
                 if (KeyName == null)
                 {
                     KeyName = GetConventionKey(For.Metadata.ModelType);
                     if (KeyName == null) throw new ArgumentException(DefaultMessages.NoRowKey, nameof(keyName));
                 }
                 InheritColumns(inheritFrom.visibleAndHiddenColumns, addColumns, removeColumns);
                 PrepareColumns();
             };
        }
        public RowType(ModelExpression expression,
            string keyName = null,
            bool isDetail = false,
            IEnumerable<Column> addColumns = null,
            bool allProperties = false,
            IEnumerable<ModelExpression> removeColumns = null,
            Func<IEnumerable<Column>, ContextualizedHelpers, object, IHtmlContent> renderHiddens = null)
        {
            IsDetail = isDetail;
            For = expression;
            this.renderHiddens = renderHiddens;
            if (KeyName == null)
            {
                KeyName = GetConventionKey(For.Metadata.ModelType);
                if (KeyName == null) throw new ArgumentException(DefaultMessages.NoRowKey, nameof(keyName));
            }
            if (!allProperties) Columns = addColumns;
            else
            {
                var standardColumns = StandardColumns(isDetail);

                InheritColumns(standardColumns, addColumns, removeColumns);
            }
            PrepareColumns();
        }
        public async Task<IHtmlContent> InvokeEdit(object o, string prefix, ContextualizedHelpers helpers)
        {
            if (EditTemplate == null) return new HtmlString(string.Empty);
                return await EditTemplate.Invoke(
                    new ModelExpression(prefix, For.ModelExplorer.GetExplorerForModel(o)),
                    this, helpers);
            
        }
        public async Task<IHtmlContent> InvokeDisplay(object o, string prefix, ContextualizedHelpers helpers)
        {
            if (DisplayTemplate == null) return new HtmlString(string.Empty);
                return await DisplayTemplate.Invoke(
                new ModelExpression(prefix, For.ModelExplorer.GetExplorerForModel(o)),
                this, helpers);
        }
        public IHtmlContent RenderHiddens(ContextualizedHelpers ctx, object rowModel)
        {
            if (renderHiddens != null) return new HtmlString(string.Empty);
            else return renderHiddens(hiddens, ctx, rowModel);
        }
        public IHtmlContent RenderKey(object rowModel)
        {
            return new HtmlString(For.Metadata.PropertyGetter(rowModel).ToString());
        }
        protected static string combinePrefixes(string p1, string p2)
        {
            return (string.IsNullOrEmpty(p1) ? p2 : (string.IsNullOrEmpty(p2) ? p1 : p1 + "." + p2));

        }
        public async Task<IHtmlContent> RenderColumn(object rowModel, Column col, bool editMode, ContextualizedHelpers ctx)
        {
            if (col.ColumnConnection == null)
            {
                var model = col.For.Metadata.PropertyGetter(rowModel);
                if (editMode) return await col.InvokeEdit(model, ctx);
                else return await col.InvokeDisplay(model, ctx);
            }
            else if (col.ColumnConnection is ColumnConnectionInfosStatic && editMode)
            {
                var model = col.For.Metadata.PropertyGetter(rowModel);
                return await col.InvokeEdit(model, ctx);
            }
            else if (!editMode)
            {
                var displayFor = col.ColumnConnection.DisplayProperty;
                var model = displayFor.Metadata.PropertyGetter(rowModel);
                var expression = new ModelExpression(combinePrefixes(col.AdditionalPrefix, displayFor.Name), displayFor.ModelExplorer.GetExplorerForModel(model));
                return await col.InvokeDisplay(ctx, expression);
            }
            else
            {
                var expression = new ModelExpression(combinePrefixes(col.AdditionalPrefix, string.Empty), For.ModelExplorer.GetExplorerForModel(rowModel));
                return await col.InvokeEdit(ctx, expression);
            }
        }

        public IHtmlContent RenderUrl(ContextualizedHelpers helpers, string actionMethod, object parameters)
        {
            if (ControllerType == null) return null;
            return new HtmlString(helpers.UrlHelper.Action(actionMethod, ControllerType.Name.Substring(0, ControllerType.Name.Length - 10)));
        }

        public IHtmlContent RenderRowAttributes(object currentRow)
        {
            return new HtmlString(string.Format(CultureInfo.InvariantCulture, "data-row:'{0}' data-key='{1}'", 
                Order,
                keyColumn.For.Metadata.PropertyGetter(currentRow)));

        }
        public IStringLocalizer GetLocalizer(IStringLocalizerFactory factory)
        {
            if (LocalizationType == null) return null;
            return factory.Create(LocalizationType);
        }
        public bool MustAddButtonColumn(ContextualizedHelpers helpers, bool editOnly=false)
        {
            return !CustomButtons && ((RequiredFunctionalities(helpers.User) & (editOnly ? Functionalities.EditOnlyHasRowButtons : Functionalities.HasRowButtons)) != 0);
                
        }

        public int VisibleColumns(ContextualizedHelpers helpers, bool editOnly = false)
        {
            return MustAddButtonColumn(helpers, editOnly) ? ColumnsCount + 1 : ColumnsCount;
        }
        private class WidthsComparer : IComparer
        {
            private int level;
            public WidthsComparer(int l)
            {
                level = l;
            }
            public int Compare(object x, object y)
            {
                var first = x as int[];
                var second = y as int[];
                return first[level] - second[level];
            }
        }
        private bool editComputed, displayComputed;
        public void ComputeWidths(bool edit, int gridMax)
        {
            lock (this)
            {
                if ((editComputed && edit) || (displayComputed && !edit)) return;
                var cols = Columns
                    .Where(m => (!m.EditOnly && !edit) || edit).ToArray();
                var levels = cols.Max(m => m.Widths != null ? m.Widths.Length : 1);
                if (levels == 0) levels = 1;
                var allWidths = new int[cols.Count()][];

                var i = 0;
                foreach (var col in cols)
                {
                    if (edit)
                        allWidths[i] = col.EditDetailWidths = new int[levels];
                    else
                        allWidths[i] = col.DisplayDetailWidths = new int[levels];
                    i++;
                }
                for (int l = 0; l < levels; l++)
                {
                    int lineStart = 0;
                    while (lineStart < allWidths.Length)
                    {
                        int lineEnd = lineStart;
                        int intSum = 0;
                        var toAdd = cols[lineEnd].GetWidth(l);
                        var convToAdd = toAdd * gridMax;
                        var intToAddB = decimal.Floor(convToAdd);
                        int intToAdd = Convert.ToInt32(convToAdd - intToAddB >= 0.5m ? decimal.Ceiling(convToAdd) : intToAddB);
                        while (intSum + intToAdd <= gridMax && lineEnd < allWidths.Length)
                        {
                            intSum += allWidths[lineEnd][l] = intToAdd;
                            lineEnd++;
                            toAdd = cols[lineEnd].GetWidth(l);
                            convToAdd = toAdd * gridMax;
                            intToAddB = decimal.Floor(convToAdd);
                            intToAdd = Convert.ToInt32(convToAdd - intToAddB >= 0.5m ? decimal.Ceiling(convToAdd) : intToAddB);
                        }
                        int globalInc = (gridMax - intSum) / (lineEnd - lineStart);
                        int minsToInc = (gridMax - intSum) % (lineEnd - lineStart);
                        if (globalInc > 0)
                        {
                            for (int j = lineStart; j < lineEnd; j++)
                                allWidths[j][l] += globalInc;
                        }
                        if (minsToInc > 0)
                        {
                            Array.Sort(allWidths, lineStart, lineEnd - lineStart, new WidthsComparer(l));
                            for (int j = lineStart; j < lineStart + minsToInc; j++)
                                allWidths[j][l]++;

                        }
                        lineStart = lineEnd;
                    }
                }
            }

        }

      }
}