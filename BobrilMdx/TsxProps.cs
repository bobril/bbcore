using System;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Njsast;

namespace BobrilMdx
{
        public class TsxProps : MarkdownObject
    {
        public TsxProps()
        {
        }

        /// <summary>
        /// Gets or sets the HTML id/identifier. May be null.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Gets or sets the CSS classes attached.
        /// </summary>
        public StructList<string> Classes { get; set; }

        /// <summary>
        /// Gets or sets the additional properties. May be null.
        /// </summary>
        public StructList<(string Key, bool Code, object? Value)> Properties { get; set; }

        /// <summary>
        /// Adds a CSS class.
        /// </summary>
        /// <param name="name">The css class name.</param>
        public void AddClass(string name)
        {
            Classes.AddUnique(name);
        }

        /// <summary>
        /// Adds a property.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        public void AddProperty(string name, string value)
        {
            Properties.Add((name, false, value));
        }

        /// <summary>
        /// Adds the specified property only if it does not already exist.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="value">The value.</param>
        public void AddPropertyIfNotExist(string name, bool code, object? value)
        {
            for (var i = 0; i < Properties.Count; i++)
            {
                if (Properties[i].Key.Equals(name, StringComparison.Ordinal))
                {
                    return;
                }
            }

            Properties.Add((name, code, value));
        }

        /// <summary>
        /// Copies/merge the values from this instance to the specified <see cref="HtmlAttributes"/> instance.
        /// </summary>
        /// <param name="props">The Props.</param>
        /// <param name="mergeIdAndProperties">If set to <c>true</c> it will merge properties to the target htmlAttributes. Default is <c>false</c></param>
        /// <param name="shared">If set to <c>true</c> it will try to share Classes and Properties if destination don't have them, otherwise it will make a copy. Default is <c>true</c></param>
        /// <exception cref="ArgumentNullException"></exception>
        public void CopyTo(TsxProps props, bool mergeIdAndProperties = false, bool shared = true)
        {
            // Add html htmlAttributes to the object
            if (!mergeIdAndProperties || Id != null)
            {
                props.Id = Id;
            }
            if (props.Classes.Count == 0)
            {
                props.Classes = shared ? Classes : new (Classes);
            }
            else
            {
                props.Classes.AddRange(Classes);
            }

            if (props.Properties.Count == 0)
            {
                props.Properties = shared ? Properties : new (Properties);
            }
            else if (Properties.Count != 0)
            {
                if (mergeIdAndProperties)
                {
                    foreach (var prop in Properties)
                    {
                        props.AddPropertyIfNotExist(prop.Key, prop.Code, prop.Value);
                    }
                }
                else
                {
                    props.Properties.AddRange(Properties);
                }
            }
        }
    }

    public static class TsxPropsExtensions
    {
        static readonly object Key = typeof (TsxProps);

        public static TsxProps? TryGetProps(this IMarkdownObject obj)
        {
            return obj.GetData(Key) as TsxProps;
        }

        public static TsxProps GetProps(this IMarkdownObject obj)
        {
            var props = obj.GetData(Key) as TsxProps;
            if (props is null)
            {
                props = new();
                obj.SetProps(props);
            }
            return props;
        }

        public static void SetProps(this IMarkdownObject obj, TsxProps props)
        {
            obj.SetData(Key, props);
        }
    }
}
