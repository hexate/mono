//
// System.Enum.cs
//
// Authors:
//   Miguel de Icaza (miguel@ximian.com)
//   Nick Drochak (ndrochak@gol.com)
//   Gonzalo Paniagua Javier (gonzalo@ximian.com)
//
// (C) Ximian, Inc.  http://www.ximian.com
//
//

//
// Copyright (C) 2004 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System.Collections;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System
{
	internal struct MonoEnumInfo
	{
		internal Type utype;
		internal Array values;
		internal string[] names;
		internal Hashtable name_hash;
		[ThreadStatic]
		static Hashtable cache;
		static Hashtable global_cache;
		static object global_cache_monitor;
		
		[MethodImplAttribute (MethodImplOptions.InternalCall)]
		private static extern void get_enum_info (Type enumType, out MonoEnumInfo info);

		//
		// These comparers are needed because enumerations must be compared
		// using unsigned values so that negative numbers can be looked up
		// See bug: #371559
		//
		internal static SByteComparer  sbyte_comparer = new SByteComparer ();
		internal static ShortComparer short_comparer = new ShortComparer ();
		internal static IntComparer   int_comparer = new IntComparer ();
		internal static LongComparer  long_comparer = new LongComparer ();
		
		internal class SByteComparer : IComparer
#if NET_2_0
	, System.Collections.Generic.IComparer<sbyte>
#endif
		{
			public int Compare (object x, object y)
			{
				sbyte ix = (sbyte) x;
				sbyte iy = (sbyte) y;
				
				return ((byte) ix) - ((byte) iy);
			}

			public int Compare (sbyte ix, sbyte iy)
			{
				return ((byte) ix) - ((byte) iy);
			}
		}
		
		internal class ShortComparer : IComparer
#if NET_2_0
	, System.Collections.Generic.IComparer<short>
#endif
	  	{
			public int Compare (object x, object y)
			{
				short ix = (short) x;
				short iy = (short) y;
				
				return ((ushort) ix) - ((ushort) iy);
			}

			public int Compare (short ix, short iy)
			{
				return ((ushort) ix) - ((ushort) iy);
			}
		}
		
		internal class IntComparer : IComparer 
#if NET_2_0
	, System.Collections.Generic.IComparer<int>
#endif
		  {
			public int Compare (object x, object y)
			{
				int ix = (int) x;
				int iy = (int) y;

				if (ix == iy)
					return 0;

				if (((uint) ix) < ((uint) iy))
					return -1;
				return 1;
			}

			public int Compare (int ix, int iy)
			{
				if (ix == iy)
					return 0;

				if (((uint) ix) < ((uint) iy))
					return -1;
				return 1;
			}
		}

		internal class LongComparer : IComparer
#if NET_2_0
	, System.Collections.Generic.IComparer<long>
#endif
		{
			public int Compare (object x, object y)
			{
				long ix = (long) x;
				long iy = (long) y;
				
				if (ix == iy)
					return 0;
				if (((ulong) ix) < ((ulong) iy))
					return -1;
				return 1;
			}

			public int Compare (long ix, long iy)
			{
				if (ix == iy)
					return 0;
				if (((ulong) ix) < ((ulong) iy))
					return -1;
				return 1;
			}
		}
			
		static MonoEnumInfo ()
		{
			global_cache_monitor = new object ();
			global_cache = new Hashtable ();
		}

		static Hashtable Cache {
			get {
				if (cache == null) {
					cache = new Hashtable ();
				}
				return cache;
			}
		}
		private MonoEnumInfo (MonoEnumInfo other)
		{
			utype = other.utype;
			values = other.values;
			names = other.names;
			name_hash = other.name_hash;
		}

		internal static void GetInfo (Type enumType, out MonoEnumInfo info)
		{
			/* First check the thread-local cache without locking */
			if (Cache.ContainsKey (enumType)) {
				info = (MonoEnumInfo) cache [enumType];
				return;
			}
			/* Threads could die, so keep a global cache too */
			lock (global_cache_monitor) {
				if (global_cache.ContainsKey (enumType)) {
					object boxedInfo = global_cache [enumType];
					cache [enumType] = boxedInfo;
					info = (MonoEnumInfo)boxedInfo;
					return;
				}
			}

			get_enum_info (enumType, out info);

			IComparer ic = null;
			if (info.values is int [])
				ic = int_comparer;
			else if (info.values is short [])
				ic = short_comparer;
			else if (info.values is sbyte [])
				ic = sbyte_comparer;
			else if (info.values is long [])
				ic = long_comparer;
			
			Array.Sort (info.values, info.names, ic);
			if (info.names.Length > 50) {
				info.name_hash = new Hashtable (info.names.Length);
				for (int i = 0; i <  info.names.Length; ++i)
					info.name_hash [info.names [i]] = i;
			}
			MonoEnumInfo cached = new MonoEnumInfo (info);
			lock (global_cache_monitor) {
				global_cache [enumType] = cached;
			}
		}
	};

	[Serializable]
#if NET_2_0
	[ComVisible (true)]
#endif
	public abstract class Enum : ValueType, IComparable, IConvertible, IFormattable
	{
		protected Enum ()
		{
		}

		// IConvertible methods Start -->
		public TypeCode GetTypeCode ()
		{
			return Type.GetTypeCode (GetUnderlyingType (this.GetType ()));
		}

		bool IConvertible.ToBoolean (IFormatProvider provider)
		{
			return Convert.ToBoolean (get_value (), provider);
		}

		byte IConvertible.ToByte (IFormatProvider provider)
		{
			return Convert.ToByte (get_value (), provider);
		}

		char IConvertible.ToChar (IFormatProvider provider)
		{
			return Convert.ToChar (get_value (), provider);
		}

		DateTime IConvertible.ToDateTime (IFormatProvider provider)
		{
			return Convert.ToDateTime (get_value (), provider);
		}

		decimal IConvertible.ToDecimal (IFormatProvider provider)
		{	
			return Convert.ToDecimal (get_value (), provider);
		}

		double IConvertible.ToDouble (IFormatProvider provider)
		{	
			return Convert.ToDouble (get_value (), provider);
		}

		short IConvertible.ToInt16 (IFormatProvider provider)
		{
			return Convert.ToInt16 (get_value (), provider);
		}

		int IConvertible.ToInt32 (IFormatProvider provider)
		{
			return Convert.ToInt32 (get_value (), provider);
		}

		long IConvertible.ToInt64 (IFormatProvider provider)
		{
			return Convert.ToInt64 (get_value (), provider);
		}

#if ONLY_1_1
#pragma warning disable 3019
		[CLSCompliant (false)]
#endif
		sbyte IConvertible.ToSByte (IFormatProvider provider)
		{
			return Convert.ToSByte (get_value (), provider);
		}
#if ONLY_1_1
#pragma warning restore 3019
#endif

		float IConvertible.ToSingle (IFormatProvider provider)
		{
			return Convert.ToSingle (get_value (), provider);
		}

		object IConvertible.ToType (Type type, IFormatProvider provider)
		{
			return Convert.ToType (get_value (), type, provider, false);
		}

#if ONLY_1_1
#pragma warning disable 3019
		[CLSCompliant (false)]
#endif
		ushort IConvertible.ToUInt16 (IFormatProvider provider)
		{
			return Convert.ToUInt16 (get_value (), provider);
		}
#if ONLY_1_1
#pragma warning restore 3019
#endif

#if ONLY_1_1
#pragma warning disable 3019
		[CLSCompliant (false)]
#endif
		uint IConvertible.ToUInt32 (IFormatProvider provider)
		{
			return Convert.ToUInt32 (get_value (), provider);
		}
#if ONLY_1_1
#pragma warning restore 3019
#endif

#if ONLY_1_1
#pragma warning disable 3019
		[CLSCompliant (false)]
#endif
		ulong IConvertible.ToUInt64 (IFormatProvider provider)
		{
			return Convert.ToUInt64 (get_value (), provider);
		}
#if ONLY_1_1
#pragma warning restore 3019
#endif

		// <-- End IConvertible methods

		[MethodImplAttribute (MethodImplOptions.InternalCall)]
		private extern object get_value ();

#if NET_2_0
		[ComVisible (true)]
#endif
		public static Array GetValues (Type enumType)
		{
			if (enumType == null)
				throw new ArgumentNullException ("enumType");

			if (!enumType.IsEnum)
				throw new ArgumentException ("enumType is not an Enum type.", "enumType");

			MonoEnumInfo info;
			MonoEnumInfo.GetInfo (enumType, out info);
			return (Array) info.values.Clone ();
		}

#if NET_2_0
		[ComVisible (true)]
#endif
		public static string[] GetNames (Type enumType)
		{
			if (enumType == null)
				throw new ArgumentNullException ("enumType");

			if (!enumType.IsEnum)
				throw new ArgumentException ("enumType is not an Enum type.");

			MonoEnumInfo info;
			MonoEnumInfo.GetInfo (enumType, out info);
			return (string []) info.names.Clone ();
		}

#if NET_2_0
		//
		// The faster, non-boxing version.   It must use the special MonoEnumInfo.xxx_comparers
		// to ensure that we are perfoming bitwise compares, and not signed compares.
		//
		// It also tries to use the non-boxing version of the various Array.BinarySearch methods
		//
		static int FindPosition (object value, Array values)
		{
			int[] int_array = values as int[];
			if (int_array != null)
				return Array.BinarySearch (int_array, (int)value, MonoEnumInfo.int_comparer);

			uint[] uint_array = values as uint [];
			if (uint_array != null)
				return Array.BinarySearch (uint_array, (uint)value);
			
			short [] short_array = values as short [];
			if (short_array != null)
				return Array.BinarySearch (short_array, (short)value, MonoEnumInfo.short_comparer);

			ushort [] ushort_array = values as ushort [];
			if (ushort_array != null)
				return Array.BinarySearch (ushort_array, (ushort)value);
					
			sbyte [] sbyte_array = values as sbyte [];
			if (sbyte_array != null)
				return Array.BinarySearch (sbyte_array, (sbyte) value,  MonoEnumInfo.sbyte_comparer);
			
			byte [] byte_array = values as byte [];
			if (byte_array != null)
				return Array.BinarySearch (byte_array, (byte) value);
			
			long [] long_array = values as long [];
			if (long_array != null)
				return Array.BinarySearch (long_array, (long) value,  MonoEnumInfo.long_comparer);

			ulong [] ulong_array = values as ulong [];
			if (ulong_array != null)
				return Array.BinarySearch (ulong_array, (ulong) value);

			// This should never happen
			return Array.BinarySearch (values, value);
		}
#else
		static int FindPosition (object value, Array values)
		{
			IComparer ic = null;

			if (values is int[])
				return Array.BinarySearch (values, value, MonoEnumInfo.int_comparer);
			if (values is short[])
				return Array.BinarySearch (values, value, MonoEnumInfo.short_comparer);
			if (values is sbyte [])
				return Array.BinarySearch (values, value,  MonoEnumInfo.sbyte_comparer);
			if (values is long [])
				return Array.BinarySearch (values, value,  MonoEnumInfo.long_comparer);

			return Array.BinarySearch (values, value);

		}
#endif
	
#if NET_2_0
		[ComVisible (true)]
#endif
		public static string GetName (Type enumType, object value)
		{
			if (enumType == null)
				throw new ArgumentNullException ("enumType");
			if (value == null)
				throw new ArgumentNullException ("value");

			if (!enumType.IsEnum)
				throw new ArgumentException ("enumType is not an Enum type.", "enumType");

			MonoEnumInfo info;
			value = ToObject (enumType, value);
			MonoEnumInfo.GetInfo (enumType, out info);

			int i = FindPosition (value, info.values);
			return (i >= 0) ? info.names [i] : null;
		}

#if NET_2_0
		[ComVisible (true)]
#endif
		public static bool IsDefined (Type enumType, object value)
		{
			if (enumType == null)
				throw new ArgumentNullException ("enumType");
			if (value == null)
				throw new ArgumentNullException ("value");

			if (!enumType.IsEnum)
				throw new ArgumentException ("enumType is not an Enum type.", "enumType");

			MonoEnumInfo info;
			MonoEnumInfo.GetInfo (enumType, out info);

			Type vType = value.GetType ();
			if (vType == typeof(String)) {
				return ((IList)(info.names)).Contains (value);
			} else if ((vType == info.utype) || (vType == enumType)) {
				value = ToObject (enumType, value);
				MonoEnumInfo.GetInfo (enumType, out info);

				return FindPosition (value, info.values) >= 0;
			} else {
				throw new ArgumentException("The value parameter is not the correct type."
					+ "It must be type String or the same type as the underlying type"
					+ "of the Enum.");
			}
		}

		[MethodImplAttribute (MethodImplOptions.InternalCall)]
		private static extern Type get_underlying_type (Type enumType);

#if NET_2_0
		[ComVisible (true)]
#endif
		public static Type GetUnderlyingType (Type enumType)
		{
			if (enumType == null)
				throw new ArgumentNullException ("enumType");

			if (!enumType.IsEnum)
				throw new ArgumentException ("enumType is not an Enum type.", "enumType");

			return get_underlying_type (enumType);
		}

#if NET_2_0
		[ComVisible (true)]
#endif
		public static object Parse (Type enumType, string value)
		{
			// Note: Parameters are checked in the other overload
			return Parse (enumType, value, false);
		}

		private static int FindName (Hashtable name_hash, string [] names, string name,  bool ignoreCase)
		{
			if (!ignoreCase) {
				/* For enums with many values, use a hash table */
				if (name_hash != null) {
					object val = name_hash [name];
					if (val != null)
						return (int)val;
				} else {
					for (int i = 0; i < names.Length; ++i) {
						if (name == names [i])
							return i;
					}
				}
			} else {
				for (int i = 0; i < names.Length; ++i) {
					if (String.Compare (name, names [i], ignoreCase, CultureInfo.InvariantCulture) == 0)
						return i;
				}
			}
			return -1;
		}

		// Helper function for dealing with [Flags]-style enums.
		private static ulong GetValue (object value, TypeCode typeCode)
		{
			switch (typeCode) {
			case TypeCode.Byte:
				return (byte) value;
			case TypeCode.SByte:
				return (byte) ((sbyte) value);
			case TypeCode.Int16:
				return (ushort) ((short) value);
			case TypeCode.Int32:
				return (uint) ((int) value);
			case TypeCode.Int64:
				return (ulong) ((long) value);
			case TypeCode.UInt16:
				return (ushort) value;
			case TypeCode.UInt32:
				return (uint) value;
			case TypeCode.UInt64:
				return (ulong) value;
			}
			throw new ArgumentException ("typeCode is not a valid type code for an Enum");
		}

		private static char [] split_char = { ',' };

#if NET_2_0
		[ComVisible (true)]
#endif
		public static object Parse (Type enumType, string value, bool ignoreCase)
		{
			if (enumType == null)
				throw new ArgumentNullException ("enumType");

			if (value == null)
				throw new ArgumentNullException ("value");

			if (!enumType.IsEnum)
				throw new ArgumentException ("enumType is not an Enum type.", "enumType");

			value = value.Trim ();
			if (value.Length == 0)
				throw new ArgumentException ("An empty string is not considered a valid value.");

			MonoEnumInfo info;
			MonoEnumInfo.GetInfo (enumType, out info);

			// is 'value' a named constant?
			int loc = FindName (info.name_hash, info.names, value, ignoreCase);
			if (loc >= 0)
				return info.values.GetValue (loc);

			TypeCode typeCode = ((Enum) info.values.GetValue (0)).GetTypeCode ();

			// is 'value' a list of named constants?
			if (value.IndexOf (',') != -1) {
				string [] names = value.Split (split_char);
				ulong retVal = 0;
				for (int i = 0; i < names.Length; ++i) {
					loc = FindName (info.name_hash, info.names, names [i].Trim (), ignoreCase);
					if (loc < 0)
						throw new ArgumentException ("The requested value was not found.");
					retVal |= GetValue (info.values.GetValue (loc), typeCode);
				}
				return ToObject (enumType, retVal);
			}

			// is 'value' a number?
#if !NET_2_0
			try {
				return ToObject (enumType, Convert.ChangeType (value, typeCode));
			} catch (FormatException) {
				throw new ArgumentException (String.Format ("The requested value '{0}' was not found.", value));
			}
#else
			switch (typeCode) {
			case TypeCode.SByte:
				sbyte sb;
				if (SByte.TryParse (value, out sb))
					return sb;
				break;
			case TypeCode.Byte:
				byte b;
				if (Byte.TryParse (value, out b))
					return b;
				break;
			case TypeCode.Int16:
				short i16;
				if (Int16.TryParse (value, out i16))
					return i16;
				break;
			case TypeCode.UInt16:
				ushort u16;
				if (UInt16.TryParse (value, out u16))
					return u16;
				break;
			case TypeCode.Int32:
				int i32;
				if (Int32.TryParse (value, out i32))
					return i32;
				break;
			case TypeCode.UInt32:
				uint u32;
				if (UInt32.TryParse (value, out u32))
					return u32;
				break;
			case TypeCode.Int64:
				long i64;
				if (Int64.TryParse (value, out i64))
					return i64;
				break;
			case TypeCode.UInt64:
				ulong u64;
				if (UInt64.TryParse (value, out u64))
					return u64;
				break;
			default:
				break;
			}
			throw new ArgumentException (String.Format ("The requested value '{0}' was not found.", value));
#endif
		}

		[MethodImplAttribute (MethodImplOptions.InternalCall)]
		private extern int compare_value_to (object other);

		/// <summary>
		///   Compares the enum value with another enum value of the same type.
		/// </summary>
		///
		/// <remarks/>
		public int CompareTo (object target)
		{
			Type thisType;

			if (target == null)
				return 1;

			thisType = this.GetType ();
			if (target.GetType() != thisType) {
				throw new ArgumentException (String.Format (
					"Object must be the same type as the enum. The type passed in was {0}; the enum type was {1}.", 
					target.GetType(), thisType));
			}

			return compare_value_to (target);
		}

		public override string ToString ()
		{
			return ToString ("G");
		}

#if NET_2_0
		[Obsolete("Provider is ignored, just use ToString")]
#endif
		public string ToString (IFormatProvider provider)
		{
			return ToString ("G", provider);
		}

		public string ToString (String format)
		{
			if (format == String.Empty || format == null)
				format = "G";
			
			return Format (this.GetType (), this.get_value (), format);
		}

#if NET_2_0
		[Obsolete("Provider is ignored, just use ToString")]
#endif
		public string ToString (String format, IFormatProvider provider)
		{
			// provider is not used for Enums

			if (format == String.Empty || format == null) {
				format = "G";
			}
			return Format (this.GetType(), this.get_value (), format);
		}

#if NET_2_0
		[ComVisible (true)]
#endif
		public static object ToObject (Type enumType, byte value)
		{
			return ToObject (enumType, (object)value);
		}

#if NET_2_0
		[ComVisible (true)]
#endif
		public static object ToObject (Type enumType, short value)
		{
			return ToObject (enumType, (object)value);
		}

#if NET_2_0
		[ComVisible (true)]
#endif
		public static object ToObject (Type enumType, int value)
		{
			return ToObject (enumType, (object)value);
		}

#if NET_2_0
		[ComVisible (true)]
#endif
		public static object ToObject (Type enumType, long value)
		{
			return ToObject (enumType, (object)value);
		}

#if NET_2_0
		[ComVisible (true)]
#endif
		[MethodImplAttribute(MethodImplOptions.InternalCall)]
		public static extern object ToObject (Type enumType, object value);

#if NET_2_0
		[ComVisible (true)]
#endif
		[CLSCompliant (false)]
		public static object ToObject (Type enumType, sbyte value)
		{
			return ToObject (enumType, (object)value);
		}

#if NET_2_0
		[ComVisible (true)]
#endif
		[CLSCompliant (false)]
		public static object ToObject (Type enumType, ushort value)
		{
			return ToObject (enumType, (object)value);
		}

#if NET_2_0
		[ComVisible (true)]
#endif
		[CLSCompliant (false)]
		public static object ToObject (Type enumType, uint value)
		{
			return ToObject (enumType, (object)value);
		}

#if NET_2_0
		[ComVisible (true)]
#endif
		[CLSCompliant (false)]
		public static object ToObject (Type enumType, ulong value)
		{
			return ToObject (enumType, (object)value);
		}

		public override bool Equals (object obj)
		{
			return DefaultEquals (this, obj);
		}

		[MethodImplAttribute (MethodImplOptions.InternalCall)]
		private extern int get_hashcode ();

		public override int GetHashCode ()
		{
			return get_hashcode ();
		}

		private static string FormatSpecifier_X (Type enumType, object value, bool upper)
		{
			switch (Type.GetTypeCode (enumType)) {
				case TypeCode.SByte:
					return ((sbyte)value).ToString (upper ? "X2" : "x2");
				case TypeCode.Byte:
					return ((byte)value).ToString (upper ? "X2" : "x2");
				case TypeCode.Int16:
					return ((short)value).ToString (upper ? "X4" : "x4");
				case TypeCode.UInt16:
					return ((ushort)value).ToString (upper ? "X4" : "x4");
				case TypeCode.Int32:
					return ((int)value).ToString (upper ? "X8" : "x8");
				case TypeCode.UInt32:
					return ((uint)value).ToString (upper ? "X8" : "x8");
				case TypeCode.Int64:
					return ((long)value).ToString (upper ? "X16" : "x16");
				case TypeCode.UInt64:
					return ((ulong)value).ToString (upper ? "X16" : "x16");
				default:
					throw new Exception ("Invalid type code for enumeration.");
			}
		}

		static string FormatFlags (Type enumType, object value)
		{
			string retVal = String.Empty;
			MonoEnumInfo info;
			MonoEnumInfo.GetInfo (enumType, out info);
			string asString = value.ToString ();
			if (asString == "0") {
				retVal = GetName (enumType, value);
				if (retVal == null)
					retVal = asString;
				return retVal;
			}
			// This is ugly, yes.  We need to handle the different integer
			// types for enums.  If someone else has a better idea, be my guest.
			switch (((Enum)info.values.GetValue (0)).GetTypeCode ()) {
			case TypeCode.SByte: {
				sbyte flags = (sbyte) value;
				sbyte enumValue;
				for (int i = info.values.Length - 1; i >= 0; i--) {
					enumValue = (sbyte) info.values.GetValue (i);
					if (enumValue == 0)
						continue;

					if ((flags & enumValue) == enumValue) {
						retVal = info.names[i] + (retVal == String.Empty ? String.Empty : ", ") + retVal;
						flags -= enumValue;
					}
				}
				if (flags != 0) return asString;
				}
				break;
			case TypeCode.Byte: {
				byte flags = (byte) value;
				byte enumValue;
				for (int i = info.values.Length - 1; i >= 0; i--) {
					enumValue = (byte) info.values.GetValue (i);
					if (enumValue == 0)
						continue;

					if ((flags & enumValue) == enumValue) {
						retVal = info.names[i] + (retVal == String.Empty ? String.Empty : ", ") + retVal;
						flags -= enumValue;
					}
				}
				if (flags != 0) return asString;
				}
				break;
			case TypeCode.Int16: {
				short flags = (short) value;
				short enumValue;
				for (int i = info.values.Length - 1; i >= 0; i--) {
					enumValue = (short) info.values.GetValue (i);
					if (enumValue == 0)
						continue;

					if ((flags & enumValue) == enumValue) {
						retVal = info.names[i] + (retVal == String.Empty ? String.Empty : ", ") + retVal;
						flags -= enumValue;
					}
				}
				if (flags != 0) return asString;
				}
				break;
			case TypeCode.Int32: {
				int flags = (int) value;
				int enumValue;
				for (int i = info.values.Length - 1; i >= 0; i--) {
					enumValue = (int) info.values.GetValue (i);
					if (enumValue == 0)
						continue;

					if ((flags & enumValue) == enumValue) {
						retVal = info.names[i] + (retVal == String.Empty ? String.Empty : ", ") + retVal;
						flags -= enumValue;
					}
				}
				if (flags != 0) return asString;
				}
				break;
			case TypeCode.UInt16: {
				ushort flags = (ushort) value;
				ushort enumValue;
				for (int i = info.values.Length - 1; i >= 0; i--) {
					enumValue = (ushort) info.values.GetValue (i);
					if (enumValue == 0)
						continue;

					if ((flags & enumValue) == enumValue) {
						retVal = info.names[i] + (retVal == String.Empty ? String.Empty : ", ") + retVal;
						flags -= enumValue;
					}
				}
				if (flags != 0) return asString;
				}
				break;
			case TypeCode.UInt32: {
				uint flags = (uint) value;
				uint enumValue;
				for (int i = info.values.Length - 1; i >= 0; i--) {
					enumValue = (uint) info.values.GetValue (i);
					if (enumValue == 0)
						continue;

					if ((flags & enumValue) == enumValue) {
						retVal = info.names[i] + (retVal == String.Empty ? String.Empty : ", ") + retVal;
						flags -= enumValue;
					}
				}
				if (flags != 0) return asString;
				}
				break;
			case TypeCode.Int64: {
				long flags = (long) value;
				long enumValue;
				for (int i = info.values.Length - 1; i >= 0; i--) {
					enumValue = (long) info.values.GetValue (i);
					if (enumValue == 0)
						continue;

					if ((flags & enumValue) == enumValue) {
						retVal = info.names[i] + (retVal == String.Empty ? String.Empty : ", ") + retVal;
						flags -= enumValue;
					}
				}
				if (flags != 0) return asString;
				}
				break;
			case TypeCode.UInt64: {
				ulong flags = (ulong) value;
				ulong enumValue;
				for (int i = info.values.Length - 1; i >= 0; i--) {
					enumValue = (ulong) info.values.GetValue (i);
					if (enumValue == 0)
						continue;

					if ((flags & enumValue) == enumValue) {
						retVal = info.names[i] + (retVal == String.Empty ? String.Empty : ", ") + retVal;
						flags -= enumValue;
					}
				}
				if (flags != 0) return asString;
				}
				break;
			}

			if (retVal == String.Empty)
				return asString;

			return retVal;
		}

#if NET_2_0
		[ComVisible (true)]
#endif
		public static string Format (Type enumType, object value, string format)
		{
			if (enumType == null)
				throw new ArgumentNullException ("enumType");
			if (value == null)
				throw new ArgumentNullException ("value");
			if (format == null)
				throw new ArgumentNullException ("format");

			if (!enumType.IsEnum)
				throw new ArgumentException ("enumType is not an Enum type.", "enumType");
			
			Type vType = value.GetType();
			Type underlyingType = Enum.GetUnderlyingType (enumType);
			if (vType.IsEnum) {
				if (vType != enumType)
					throw new ArgumentException (string.Format(CultureInfo.InvariantCulture,
						"Object must be the same type as the enum. The type" +
						" passed in was {0}; the enum type was {1}.",
						vType.FullName, enumType.FullName));
			} else if (vType != underlyingType) {
				throw new ArgumentException (string.Format (CultureInfo.InvariantCulture,
					"Enum underlying type and the object must be the same type" +
					" or object. Type passed in was {0}; the enum underlying" +
					" type was {1}.", vType.FullName, underlyingType.FullName));
			}

			if (format.Length != 1)
				throw new FormatException ("Format String can be only \"G\",\"g\",\"X\"," + 
					"\"x\",\"F\",\"f\",\"D\" or \"d\".");

			char formatChar = format [0];
			string retVal;
			if ((formatChar == 'G' || formatChar == 'g')) {
				if (!enumType.IsDefined (typeof(FlagsAttribute), false)) {
					retVal = GetName (enumType, value);
					if (retVal == null)
						retVal = value.ToString();

					return retVal;
				}

				formatChar = 'f';
			}
			
			if ((formatChar == 'f' || formatChar == 'F'))
				return FormatFlags (enumType, value);

			retVal = String.Empty;
			switch (formatChar) {
			case 'X':
				retVal = FormatSpecifier_X (enumType, value, true);
				break;
			case 'x':
				retVal = FormatSpecifier_X (enumType, value, false);
				break;
			case 'D':
			case 'd':
				if (underlyingType == typeof (ulong)) {
					ulong ulongValue = Convert.ToUInt64 (value);
					retVal = ulongValue.ToString ();
				} else {
					long longValue = Convert.ToInt64 (value);
					retVal = longValue.ToString ();
				}
				break;
			default:
				throw new FormatException ("Format String can be only \"G\",\"g\",\"X\"," + 
					"\"x\",\"F\",\"f\",\"D\" or \"d\".");
			}
			return retVal;
		}
	}
}
