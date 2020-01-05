using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using Njsast.Ast;

namespace Njsast.Runtime
{
    public class TypeConverter
    {
        public static AstNode ToAst(object o)
        {
            if (o is AstNode node) return node;
            if (o is string str) return new AstString(str);
            if (o is double num)
            {
                if (double.IsNaN(num)) return AstNaN.Instance;
                if (double.IsPositiveInfinity(num)) return AstInfinity.Instance;
                if (double.IsNegativeInfinity(num)) return AstInfinity.NegativeInstance;
                return new AstNumber(num);
            }

            if (o is int i)
            {
                return new AstNumber(i);
            }

            if (o is uint u)
            {
                return new AstNumber(u);
            }

            if (o is bool b) return b ? AstTrue.Instance : (AstNode) AstFalse.Instance;
            if (o is Dictionary<object, object> dict)
            {
                var res = new AstObject();
                foreach (var pair in dict)
                {
                    res.Properties.Add(new AstObjectKeyVal(ToAst(pair.Key), ToAst(pair.Value)));
                }

                return res;
            }

            if (o is IList<object> list)
            {
                var res = new AstArray();
                foreach (var item in list)
                {
                    res.Elements.Add(ToAst(item));
                }

                return res;
            }

            throw new NotImplementedException();
        }

        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.2
        public static bool ToBoolean(object o)
        {
            switch (o)
            {
                case bool b:
                    return b;
                case AstNull _:
                case AstUndefined _:
                case AstNaN _:
                case AstFalse _:
                    return false;
                case double d:
                    // ReSharper disable once CompareOfFloatsByEqualityOperator
                    return (d != 0 && !double.IsNaN(d));
                case string s:
                    return s.Length != 0;
                case int i:
                    return i != 0;
                case uint u:
                    return u != 0;
                case AstTrue _:
                case AstInfinity _:
                    return true;
                case AstNumber number:
                    // ReSharper disable once CompareOfFloatsByEqualityOperator
                    return (number.Value != 0 && !double.IsNaN(number.Value));
                case AstString str:
                    return str.Value.Length != 0;
                default:
                    return true;
            }
        }

        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.3
        public static double ToNumber(object? o)
        {
            switch (o)
            {
                case double d:
                    return d;
                case bool b:
                    return b ? 1 : 0;
                case int i:
                    return i;
                case uint u:
                    return u;
                case AstInfinity _:
                    return double.PositiveInfinity;
                case AstUndefined _:
                    return double.NaN;
                case AstNaN _:
                    return double.NaN;
                case AstNull _:
                    return 0;
                case string s:
                    return ToNumber(s);
                default:
                    throw new ArgumentOutOfRangeException(nameof(o), o, "Cannot ToNumber");
            }
        }

        static double ToNumber(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return 0;
            }

            var first = input[0];
            if (input.Length == 1 && first >= '0' && first <= '9')
            {
                return first - '0';
            }

            ReadOnlySpan<char> s = input;
            while (s.Length > 0 && IsWhiteSpaceEx(s[0]))
            {
                s = s.Slice(1);
            }

            while (s.Length > 0 && IsWhiteSpaceEx(s[s.Length - 1]))
            {
                s = s.Slice(0, s.Length - 1);
            }

            if (s.Length == 0)
            {
                return 0;
            }

            if (s.Length == 8 || s.Length == 9)
            {
                if ("+Infinity" == s || "Infinity" == s)
                {
                    return double.PositiveInfinity;
                }

                if ("-Infinity" == s)
                {
                    return double.NegativeInfinity;
                }
            }

            try
            {
                if (s.Length > 2 && s[0] == '0' && char.IsLetter(s[1]))
                {
                    int fromBase = 0;
                    if (s[1] == 'x' || s[1] == 'X')
                    {
                        fromBase = 16;
                    }

                    if (s[1] == 'o' || s[1] == 'O')
                    {
                        fromBase = 8;
                    }

                    if (s[1] == 'b' || s[1] == 'B')
                    {
                        fromBase = 2;
                    }

                    if (fromBase > 0)
                    {
                        return Convert.ToInt32(s.Slice(2).ToString(), fromBase);
                    }
                }

                var start = s[0];
                if (start != '+' && start != '-' && start != '.' && !char.IsDigit(start))
                {
                    return double.NaN;
                }

                var n = double.Parse(s,
                    NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign |
                    NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite |
                    NumberStyles.AllowExponent, CultureInfo.InvariantCulture);
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (s.StartsWith("-") && n == 0)
                {
                    return -0.0;
                }

                return n;
            }
            catch (OverflowException)
            {
                return s.StartsWith("-") ? double.NegativeInfinity : double.PositiveInfinity;
            }
            catch
            {
                return double.NaN;
            }
        }

        const char BOM = '\uFEFF';

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsWhiteSpaceEx(char c)
        {
            return char.IsWhiteSpace(c) || c == BOM;
        }

        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.5
        public static int ToInt32(object o)
        {
            return (int) (uint) ToNumber(o);
        }

        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.6
        public static uint ToUint32(object o)
        {
            return (uint) ToNumber(o);
        }

        public static string ToString(double d)
        {
            if (double.IsNaN(d)) return "NaN";
            if (double.IsInfinity(d)) return double.IsPositiveInfinity(d) ? "Infinity" : "-Infinity";
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (d == 0) return "0";
            return d.ToString("R", CultureInfo.InvariantCulture);
        }

        /// http://www.ecma-international.org/ecma-262/6.0/#sec-tostring
        public static string ToString(object? o)
        {
            switch (o)
            {
                case string s:
                    return s;
                case AstString astString:
                    return astString.Value;
                case bool b:
                    return b ? "true" : "false";
                case AstTrue _:
                    return "true";
                case AstFalse _:
                    return "false";
                case double d:
                    return ToString(d);
                case AstNumber astNumber:
                    return ToString(astNumber.Value);
                case int i:
                    return i.ToString();
                case uint u:
                    return u.ToString();
                case AstUndefined _:
                    return "undefined";
                case AstNull _:
                    return "null";
                case Dictionary<object, object> _:
                    return "[object Object]";
                case AstNaN _:
                    return "NaN";
                case AstInfinity _:
                    return "Infinity";
                case null:
                    throw new ArgumentNullException(nameof(o));
                default:
                    return ToString(ToPrimitiveString(o));
            }
        }

        public static object? ToPrimitiveString(object o)
        {
            return ToPrimitive(o);
        }

        public static object? ToPrimitive(object o)
        {
            switch (o)
            {
                case string s:
                    return s;
                case AstString astString:
                    return astString.Value;
                case bool _:
                    return o;
                case AstTrue _:
                    return AstTrue.BoxedTrue;
                case AstFalse _:
                    return AstFalse.BoxedFalse;
                case double _:
                    return o;
                case AstNumber astNumber:
                    return astNumber.Value;
                case int i:
                    return (double) i;
                case uint u:
                    return (double) u;
                case AstUndefined _:
                    return o;
                case AstNull _:
                    return o;
                case Dictionary<object, object> _:
                    return "[object Object]";
                case AstNaN _:
                    return AstNaN.BoxedNaN;
                case AstInfinity _:
                    return AstInfinity.BoxedInfinity;
                default:
                    return null;
            }
        }

        public static JsType GetJsType(object o)
        {
            switch (o)
            {
                case string _:
                    return JsType.String;
                case AstString _:
                    return JsType.String;
                case bool _:
                    return JsType.Boolean;
                case AstTrue _:
                    return JsType.Boolean;
                case AstFalse _:
                    return JsType.Boolean;
                case double _:
                    return JsType.Number;
                case AstNumber _:
                    return JsType.Number;
                case int _:
                    return JsType.Number;
                case uint _:
                    return JsType.Number;
                case AstUndefined _:
                    return JsType.Undefined;
                case AstNull _:
                    return JsType.Null;
                case Dictionary<object, object> _:
                    return JsType.Object;
                case AstNaN _:
                    return JsType.Number;
                case AstInfinity _:
                    return JsType.Number;
                default:
                    return JsType.Object;
            }
        }
    }
}
