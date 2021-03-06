﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using MvcControlsToolkit.Core.TagHelpersUtilities;
using MvcControlsToolkit.Core.Templates;

namespace MvcControlsToolkit.Core.TagHelpers
{
    public static class TagContextHelper
    {
        private const string bodyKey = "__body__";
        private const string formKey = "__form__";
        private const string rowContainerKey = "__row_container__";
        private const string bindingKeyPrefix = "__binding__";
        private const string typeBindingKeyPrefix = "__type_binding__";
        
        public static void OpenRowContainerContext(HttpContext httpContext)
        {
            RenderingContext.OpenContext<Tuple<IList<RowType>, IList<KeyValuePair<string, string>>>>(httpContext, rowContainerKey, null);
        }
        public static void CloseRowContainerContext(HttpContext httpContext, Tuple<IList<RowType>, IList<KeyValuePair<string, string>>> group)
        {
            RenderingContext.CloseContext<Tuple<IList<RowType>, IList<KeyValuePair<string, string>>>>(httpContext, rowContainerKey, group);
        }
        public static void RegisterRowsDependency(HttpContext httpContext, Action<IList<RowType>> action)
        {
            RenderingContext.AttachEvent<IList<RowType>>(httpContext, rowContainerKey, (r,o) => { action(r); });
        }
        public static void OpenBodyContext(HttpContext httpContext)
        {
            RenderingContext.OpenContext<Action<IHtmlContent, object>>(httpContext, bodyKey, (s, o) =>
            {
                (o as TagHelperOutput).PostContent.AppendHtml(s);
            });
        }
        public static void EndOfBodyHtml(HttpContext httpContext, IHtmlContent html)
        {
            var res = RenderingContext.Current(httpContext, bodyKey);
            if (res == null || res.Empty) OpenBodyContext(httpContext);
            RenderingContext.AttachEvent<Action<IHtmlContent, object>>(httpContext, bodyKey, 
                (f, o) =>
                {
                    f(html, o);
                }
                );
        }
        public static void CloseBodyContext(HttpContext httpContext, TagHelperOutput o)
        {
            RenderingContext.CloseContext(o, httpContext, bodyKey);
        }
        public static void OpenFormContext(HttpContext httpContext)
        {
            if (DisabledPostFormContent.IsDisabled(httpContext)) return;
            RenderingContext.OpenContext<Action<IHtmlContent, object>>(httpContext, formKey, (s, o) =>
            {
                (o as TagHelperOutput).PostElement.AppendHtml(s);
            });
        }
       
        public static void EndOfFormHtml(HttpContext httpContext, IHtmlContent html)
        {
            if (DisabledPostFormContent.IsDisabled(httpContext)) return;
            var res = RenderingContext.Current(httpContext, formKey);
            if (res == null || res.Empty) OpenFormContext(httpContext);
            RenderingContext.AttachEvent<Action<IHtmlContent, object>>(httpContext, formKey,
                (f, o) =>
                {
                    f(html, o);
                }
                );
        }
        public static void CloseFormContext(HttpContext httpContext, TagHelperOutput o)
        {
            if (DisabledPostFormContent.IsDisabled(httpContext)) return;
            RenderingContext.CloseContext(o, httpContext, formKey);
        }
        public static void RegisterDefaultToolWindow(HttpContext httpContext, Func<Tuple<IList<RowType>, IList<KeyValuePair<string, string>>>, IHtmlContent> getHtml)
        {
            RenderingContext.AttachEvent<Tuple<IList<RowType>, IList<KeyValuePair<string, string>>>>(httpContext, rowContainerKey,
                (groups, o) =>
                {
                    EndOfBodyHtml(httpContext, getHtml(groups));
                }
                );
        }
        public static void RegisterDefaultFormToolWindow(HttpContext httpContext, Func<Tuple<IList<RowType>, IList<KeyValuePair<string, string>>>, IHtmlContent> getHtml)
        {
            RenderingContext.AttachEvent<Tuple<IList<RowType>, IList<KeyValuePair<string, string>>>>(httpContext, rowContainerKey,
                (groups, o) =>
                {
                    EndOfFormHtml(httpContext, getHtml(groups));
                }
                );
        }
        public static void OpenBindingContext(HttpContext httpContext, string name, ModelExpression data)
        {
            RenderingContext.OpenContext<ModelExpression>(httpContext, bindingKeyPrefix+name, data);
        }
        public static void CloseBindingContext(HttpContext httpContext, string name)
        {
            RenderingContext.CloseContext(httpContext, bindingKeyPrefix + name);
        }
        public static ModelExpression GetBindingContext(HttpContext httpContext, string name)
        {
            return RenderingContext.CurrentData<ModelExpression>(httpContext, bindingKeyPrefix + name);
        }
        public static void OpenTypeBindingContext(HttpContext httpContext, string name, Type data)
        {
            RenderingContext.OpenContext<Type>(httpContext, typeBindingKeyPrefix + name, data);
        }
        public static void CloseTypeBindingContext(HttpContext httpContext, string name)
        {
            RenderingContext.CloseContext(httpContext, typeBindingKeyPrefix + name);
        }
        public static Type GetTypeBindingContext(HttpContext httpContext, string name)
        {
            return RenderingContext.CurrentData<Type>(httpContext, typeBindingKeyPrefix + name);
        }
    }
}
