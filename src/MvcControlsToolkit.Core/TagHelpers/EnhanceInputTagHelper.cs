﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Razor.TagHelpers;
using Microsoft.AspNet.Mvc.TagHelpers;
using Microsoft.AspNet.Mvc.Rendering;
using System.Globalization;
using System.ComponentModel.DataAnnotations;

namespace MvcControlsToolkit.Core.TagHelpers
{
    [HtmlTargetElement("input", Attributes = ForAttributeName, TagStructure = TagStructure.WithoutEndTag)]
    public class EnhanceInputTagHelper: TagHelper 
    {
        private const string ForAttributeName = "asp-for";
        private static string[] positiveIntegerTypes = new string[] {nameof(Byte).ToLowerInvariant(), nameof(UInt16).ToLowerInvariant(), nameof(UInt32).ToLowerInvariant(), nameof(UInt64).ToLowerInvariant() };
        private static string[] integerTypes = new string[] { nameof(SByte).ToLowerInvariant(), nameof(Int16).ToLowerInvariant(), nameof(Int32).ToLowerInvariant(), nameof(Int64).ToLowerInvariant() };
        public override int Order
        {
            get
            {
                return int.MinValue;
            }
        }
        [HtmlAttributeName("type")]
        public string InputTypeName { get; set; }

        [HtmlAttributeName("min")]
        public string Min { get; set; }

        [HtmlAttributeName("max")]
        public string Max { get; set; }

        [HtmlAttributeName(ForAttributeName)]
        public ModelExpression For { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            var modelExplorer = For.ModelExplorer;
            InputTypeName = InputTypeName == null ? null : InputTypeName.Trim().ToLowerInvariant();
            string min = string.IsNullOrEmpty(Min)? null : Min, max = string.IsNullOrEmpty(Max)? null : Max;
            var metaData = modelExplorer.Metadata;
            var typeName = modelExplorer.Metadata.UnderlyingOrModelType.Name.ToLowerInvariant();
            var hint = (metaData.DataTypeName ?? metaData.TemplateHint)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(InputTypeName) && !output.Attributes.ContainsName("type"))
            {
                string type=null;
                if (hint == "color") type = hint;
                else if (typeName == "single" || typeName == "double" || typeName == "decimal") type = "number";
                else if (typeName == "week" || typeName == "month") type = typeName;
                
                if(type != null) output.Attributes["type"] = type;
                
            }
            bool isDecimal = typeName == "single" || typeName == "double" || typeName == "decimal";
            if (isDecimal) output.Attributes["data-decimal"] = "true";
            bool isNumber = (string.IsNullOrEmpty(InputTypeName) || InputTypeName == "number" || InputTypeName == "range");
            bool isPositive = positiveIntegerTypes.Contains(typeName);
            bool isIntegerNP = integerTypes.Contains(typeName);
            bool isHtml5DateTime = (string.IsNullOrEmpty(InputTypeName) || InputTypeName == "date" || InputTypeName == "datetime" || InputTypeName == "datetime-local" || InputTypeName == "week" || InputTypeName == "month");
            bool isDateTimeType = typeName == "datetime" || typeName == "timespan" || typeName == "week" || typeName == "month";
            RangeAttribute limits = metaData.ValidatorMetadata.Where(m => m is RangeAttribute).FirstOrDefault() as RangeAttribute;

            if (limits != null)
            {
                if (min == null && limits.Minimum != null)
                {
                    if (isNumber && isPositive)
                    {

                        var trueMin = ((limits.Minimum is string) ? Convert.ChangeType(limits, limits.OperandType, CultureInfo.InvariantCulture) : limits.Minimum) as IComparable;
                        if (trueMin.CompareTo(Convert.ChangeType("0", limits.OperandType)) < 0) min = "0";
                        else min = (trueMin as IFormattable).ToString(null, CultureInfo.InvariantCulture);


                    }
                    else if (isNumber && (isIntegerNP || isDecimal))
                    {
                        min = (limits.Minimum is string) ? limits.Minimum as string : (limits.Minimum as IConvertible).ToString(CultureInfo.InvariantCulture);
                    }
                    else if(isHtml5DateTime && isDateTimeType)
                    {
                        min = limits.Minimum as string;
                    }
                }
                if (max == null && limits.Maximum != null)
                {
                    if (isNumber && (isPositive || isIntegerNP || isDecimal))
                        max = (limits.Maximum is string) ? limits.Maximum as string : (limits.Maximum as IConvertible).ToString(CultureInfo.InvariantCulture);
                    else if(isHtml5DateTime && isDateTimeType)
                    {
                        max = limits.Maximum as string;
                    }
                }
            }

            if (isNumber && isPositive && min == null)
            {
                min = "0";
            }
            if (min != null) output.Attributes["min"] = min;
            if (max != null) output.Attributes["max"] = max;

        }
    }
}