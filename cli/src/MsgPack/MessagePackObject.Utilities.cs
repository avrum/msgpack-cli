﻿#region -- License Terms --
//
// MessagePack for CLI
//
// Copyright (C) 2010 FUJIWARA, Yusuke
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//
#endregion -- License Terms --

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
//using Microsoft.CSharp.RuntimeBinder;

namespace MsgPack
{
#warning TODO: Recursive list serialization
#warning TODO: Recursive dictionary serialization
#warning TODO: Improve UTF-8 String
#warning TODO: Improve Dictionary<MPO,MPO> usability
	partial struct MessagePackObject
#if !SILVERLIGHT
 : ISerializable
#endif
	{
		#region -- Type Code Constants --

		private static readonly ValueTypeCode _sbyteTypeCode = new ValueTypeCode( typeof( sbyte ) );
		private static readonly ValueTypeCode _byteTypeCode = new ValueTypeCode( typeof( byte ) );
		private static readonly ValueTypeCode _int16TypeCode = new ValueTypeCode( typeof( short ) );
		private static readonly ValueTypeCode _uint16TypeCode = new ValueTypeCode( typeof( ushort ) );
		private static readonly ValueTypeCode _int32TypeCode = new ValueTypeCode( typeof( int ) );
		private static readonly ValueTypeCode _uint32TypeCode = new ValueTypeCode( typeof( uint ) );
		private static readonly ValueTypeCode _int64TypeCode = new ValueTypeCode( typeof( long ) );
		private static readonly ValueTypeCode _uint64TypeCode = new ValueTypeCode( typeof( ulong ) );
		private static readonly ValueTypeCode _singleTypeCode = new ValueTypeCode( typeof( float ) );
		private static readonly ValueTypeCode _doubleTypeCode = new ValueTypeCode( typeof( double ) );
		private static readonly ValueTypeCode _booleanTypeCode = new ValueTypeCode( typeof( bool ) );

		#endregion -- Type Code Constants --

		/// <summary>
		///		Instance represents nil. This is equal to default value.
		/// </summary>
		public static readonly MessagePackObject Nil = new MessagePackObject();

		#region -- Type Code Fields & Properties --

		private readonly object _handleOrTypeCode;

		/// <summary>
		///		Get whether this instance represents nil.
		/// </summary>
		/// <value>If this instance represents nil object, then true.</value>
		public bool IsNil
		{
			get { return this._handleOrTypeCode == null; }
		}

		private readonly ulong _value;

		#endregion -- Type Code Fields & Properties --

		#region -- Constructors --

		/// <summary>
		///		Initialize new instance wraps <see cref="String"/>.
		/// </summary>
		public MessagePackObject( string value )
		{
			// trick: Avoid long boilerplate initialization. See "CLR via C#".
			this = new MessagePackObject();
			this._handleOrTypeCode = new MessagePackString( value );
		}

		/// <summary>
		///		Initialize new instance wraps <see cref="IList&lt;MessagePackObject&gt;"/>.
		/// </summary>
		public MessagePackObject( IList<MessagePackObject> value )
		{
			// trick: Avoid long boilerplate initialization. See "CLR via C#".
			this = new MessagePackObject();
			this._handleOrTypeCode = value;
		}

		/// <summary>
		///		Initialize new instance wraps <see cref="IDictionary&lt;MessagePackObject, MessagePackObject&gt;"/>.
		/// </summary>
		public MessagePackObject( IDictionary<MessagePackObject, MessagePackObject> value )
		{
			// trick: Avoid long boilerplate initialization. See "CLR via C#".
			this = new MessagePackObject();
			this._handleOrTypeCode = value;
		}

		/// <summary>
		///		Initialize new instance wraps raw byte array.
		/// </summary>
		public MessagePackObject( byte[] value )
			: this( value, false ) { }

		/// <summary>
		///		Initialize new instance wraps raw byte array.
		/// </summary>
		internal MessagePackObject( byte[] value, bool mayString )
		{
			// trick: Avoid long boilerplate initialization. See "CLR via C#".
			this = new MessagePackObject();
			this._handleOrTypeCode = new MessagePackString( value, mayString );
		}

		#endregion -- Constructors --

#if !SILVERLIGHT

		#region -- ISerializable support --

		/// <summary>
		///		Serialize this instance.
		/// </summary>
		/// <param name="info"><see cref="SerializationInfo"/> to store serialized data.</param>
		/// <param name="context"><see cref="StreamingContext"/> which stores context information.</param>
		/// <exception cref="ArgumentNullException">
		///		<paramref name="info"/> is null.
		/// </exception>
		public void GetObjectData( SerializationInfo info, StreamingContext context )
		{
			if ( info == null )
			{
				throw new ArgumentNullException( "info" );
			}

			Contract.EndContractBlock();

			if ( this.IsNil )
			{
				info.AddValue( "TypeCode", TypeCode.Empty );
				return;
			}

			var typeCode = this._handleOrTypeCode as ValueTypeCode;
			if ( typeCode != null )
			{
				info.AddValue( "TypeCode", typeCode.TypeCode );
				info.AddValue( "Value", this._value );
			}
			else
			{
				info.AddValue( "TypeCode", TypeCode.Object );
				info.AddValue( "Value", this._handleOrTypeCode, this._handleOrTypeCode.GetType() );
			}
		}

		#endregion -- ISerializable support --

#endif

		#region -- Structure Methods --

		/// <summary>
		///		Compare two instances are equal.
		/// </summary>
		/// <param name="obj"><see cref="MessagePackObject"/> instance.</param>
		/// <returns>
		///		If <paramref name="obj"/> is <see cref="MessagePackObject"/> and its value is equal to this instance, then true.
		///		Otherwise false.
		/// </returns>
		public override bool Equals( Object obj )
		{
			if ( obj == null || !( obj is MessagePackObject ) )
			{
				return false;
			}
			else
			{
				return this.Equals( ( MessagePackObject )obj );
			}
		}

		/// <summary>
		///		Compare two instances are equal.
		/// </summary>
		/// <param name="other"><see cref="MessagePackObject"/> instance.</param>
		/// <returns>
		///		Whether value of <paramref name="other"/> is equal to this instance or not.
		/// </returns>
		public bool Equals( MessagePackObject other )
		{
			if ( this._handleOrTypeCode == null )
			{
				return other._handleOrTypeCode == null;
			}
			else if ( other._handleOrTypeCode == null )
			{
				return false;
			}

			var valueTypeCode = this._handleOrTypeCode as ValueTypeCode;
			if ( valueTypeCode != null )
			{
				switch ( valueTypeCode.TypeCode )
				{
					case TypeCode.Boolean:
					case TypeCode.Single:
					case TypeCode.Double:
					{
						if ( !Object.ReferenceEquals( this._handleOrTypeCode, other._handleOrTypeCode ) )
						{
							return false;
						}

						break;
					}
				}

				return this._value == other._value;
			}

			{
				var asArray = this._handleOrTypeCode as IList<MessagePackObject>;
				if ( asArray != null )
				{
					var otherAsArray = other._handleOrTypeCode as IList<MessagePackObject>;
					if ( otherAsArray == null )
					{
						return false;
					}

					return asArray.SequenceEqual( otherAsArray );
				}
			}

			{
				var asMap = this._handleOrTypeCode as IDictionary<MessagePackObject, MessagePackObject>;
				if ( asMap != null )
				{
					var otherAsMap = other._handleOrTypeCode as IDictionary<MessagePackObject, MessagePackObject>;
					if ( otherAsMap == null )
					{
						return false;
					}

					if ( asMap.Count != otherAsMap.Count )
					{
						return false;
					}

					foreach ( var kv in asMap )
					{
						MessagePackObject value;
						if ( !otherAsMap.TryGetValue( kv.Key, out value ) )
						{
							return false;
						}

						if ( kv.Value != value )
						{
							return false;
						}
					}

					return true;
				}
			}

			{
				var asMps = this._handleOrTypeCode as MessagePackString;
				if ( asMps != null )
				{
					return asMps.Equals( other._handleOrTypeCode as MessagePackString );
				}
			}

			Debug.Assert( false, String.Format( "Unkown handle type '{0}'(value: '{1}')", this._handleOrTypeCode.GetType(), this._handleOrTypeCode ) );
			return this._handleOrTypeCode.Equals( other._handleOrTypeCode );
		}

		/// <summary>
		///		Get hash code of this instance.
		/// </summary>
		/// <returns>Hash code of this instance.</returns>
		public override int GetHashCode()
		{
			if ( this._handleOrTypeCode == null )
			{
				return 0;
			}

			if ( this._handleOrTypeCode is ValueTypeCode )
			{
				return this._value.GetHashCode();
			}

			{
				var asArray = this._handleOrTypeCode as IList<MessagePackObject>;
				if ( asArray != null )
				{
					// TODO: big array support...
					return asArray.Aggregate( 0, ( hash, item ) => hash ^ item.GetHashCode() );
				}
			}

			{
				var asMap = this._handleOrTypeCode as IDictionary<MessagePackObject, MessagePackObject>;
				if ( asMap != null )
				{
					// TODO: big map support...
					return asMap.Aggregate( 0, ( hash, item ) => hash ^ item.GetHashCode() );
				}
			}

			var asMps = this._handleOrTypeCode as MessagePackString;
			if ( asMps != null )
			{
				return asMps.GetHashCode();
			}
			else
			{
				Contract.Assert( false, String.Format( "(this._handleOrTypeCode is string) but {0}", this._handleOrTypeCode.GetType() ) );
				return 0;
			}
		}

		/// <summary>
		///		Get string representation of this object.
		/// </summary>
		/// <returns>String representation of this object.</returns>
		/// <remarks>
		///		<note>
		///			DO NOT use this value programmically. 
		///			The purpose of this method is informational, so format of this value subject to change.
		///		</note>
		/// </remarks>
		public override string ToString()
		{
			if ( this._handleOrTypeCode == null )
			{
				return String.Empty;
			}

			if ( this._handleOrTypeCode is ValueTypeCode )
			{
				return this._value.ToString();
			}

			{
				var asArray = this._handleOrTypeCode as IList<MessagePackObject>;
				if ( asArray != null )
				{
					// TODO: big array support...
					if ( asArray.Count == 0 )
					{
						return "[]";
					}

					var sb = new StringBuilder( "[" ).Append( asArray[ 0 ] );
					return asArray.Skip( 1 ).Aggregate( sb, ( buffer, item ) => buffer.Append( ", " ).Append( item.ToString() ) ).Append( "]" ).ToString();
				}
			}

			{
				var asMap = this._handleOrTypeCode as IDictionary<MessagePackObject, MessagePackObject>;
				if ( asMap != null )
				{
					// TODO: big map support...
					if ( asMap.Count == 0 )
					{
						return "{}";
					}

					var first = asMap.First();
					var sb = new StringBuilder( "{" ).Append( first.Key.ToString() ).Append( " : " ).Append( first.Value.ToString() );
					return asMap.Skip( 1 ).Aggregate( sb, ( buffer, item ) => buffer.Append( ", " ).Append( item.Key.ToString() ).Append( " : " ).Append( item.Value.ToString() ) ).Append( "}" ).ToString();
				}
			}

			{
				var asBinary = this._handleOrTypeCode as MessagePackString;
				if ( asBinary != null )
				{
					// TODO: big array support...
					var asString = asBinary.TryGetString();
					if ( asString != null )
					{
						return asString;
					}

					var asBlob = asBinary.UnsafeGetBuffer();
					if ( asBlob != null )
					{
						return BitConverter.ToString( asBlob );
					}

					return String.Empty;
				}
			}

			// may be string
			Contract.Assert( false, String.Format( "(this._handleOrTypeCode is string) but {0}", this._handleOrTypeCode.GetType() ) );
			return this._handleOrTypeCode.ToString();
		}

		#endregion -- Structure Methods --

		#region -- Type Of Methods --

		/// <summary>
		///		Determine whether the underlying value of this instance is specified type or not.
		/// </summary>
		/// <typeparam name="T">Target type.</typeparam>
		/// <returns>If the underlying value of this instance is <typeparamref name="T"/> then true, otherwise false.</returns>
		public bool? IsTypeOf<T>()
		{
			return this.IsTypeOf( typeof( T ) );
		}

		/// <summary>
		///		Determine whether the underlying value of this instance is specified type or not.
		/// </summary>
		/// <param name="type">Target type.</param>
		/// <returns>If the underlying value of this instance is <paramref name="type"/> then true, otherwise false.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="type"/> is null.</exception>
		public bool? IsTypeOf( Type type )
		{
			if ( type == null )
			{
				throw new ArgumentNullException( "type" );
			}

			Contract.EndContractBlock();

			if ( this._handleOrTypeCode == null )
			{
				return null;
			}

			var typeCode = this._handleOrTypeCode as ValueTypeCode;
			if ( typeCode == null )
			{
				if ( type == typeof( string ) )
				{
					return this._handleOrTypeCode is MessagePackString;
				}

				if ( type == typeof( byte[] ) )
				{
					return this._handleOrTypeCode is MessagePackString;
				}

				// Can IEnumerable<byte>
				if ( typeof( IEnumerable<MessagePackObject> ).IsAssignableFrom( type ) 
					&& this._handleOrTypeCode is MessagePackString )
				{
					return true;
				}
				
				return type.IsAssignableFrom( this._handleOrTypeCode.GetType() );
			}

			// Lifting support.
			switch ( Type.GetTypeCode( type ) )
			{
				case TypeCode.SByte:
				{
					return
						typeCode.Type == typeof( sbyte )
						|| ( typeCode.Type == typeof( byte ) && this._value < 0x80 );
				}
				case TypeCode.Byte:
				{
					return
						( typeCode.Type == typeof( sbyte ) && this._value < 0x80 )
						|| typeCode.Type == typeof( byte );
				}
				case TypeCode.Int16:
				{
					return
						typeCode.Type == typeof( sbyte )
						|| ( typeCode.Type == typeof( byte ) && this._value < 0x80 )
						|| typeCode.Type == typeof( short )
						|| ( typeCode.Type == typeof( ushort ) && this._value < 0x8000 );
				}
				case TypeCode.UInt16:
				{
					return
						( typeCode.Type == typeof( sbyte ) && this._value < 0x80 )
						|| typeCode.Type == typeof( byte )
						|| ( typeCode.Type == typeof( short ) && this._value < 0x8000 )
						|| typeCode.Type == typeof( ushort );
				}
				case TypeCode.Int32:
				{
					return
						typeCode.Type == typeof( sbyte )
						|| ( typeCode.Type == typeof( byte ) && this._value < 0x80 )
						|| typeCode.Type == typeof( short )
						|| ( typeCode.Type == typeof( ushort ) && this._value < 0x8000 )
						|| typeCode.Type == typeof( int )
						|| ( typeCode.Type == typeof( uint ) && this._value < 0x80000000 );
				}
				case TypeCode.UInt32:
				{
					return
						( typeCode.Type == typeof( sbyte ) && this._value < 0x80 )
						|| typeCode.Type == typeof( byte )
						|| ( typeCode.Type == typeof( short ) && this._value < 0x8000 )
						|| typeCode.Type == typeof( ushort )
						|| ( typeCode.Type == typeof( int ) && this._value < 0x80000000 )
						|| typeCode.Type == typeof( uint );
				}
				case TypeCode.Int64:
				{
					return
						typeCode.Type == typeof( sbyte )
						|| ( typeCode.Type == typeof( byte ) && this._value < 0x80 )
						|| typeCode.Type == typeof( short )
						|| ( typeCode.Type == typeof( ushort ) && this._value < 0x8000 )
						|| typeCode.Type == typeof( int )
						|| ( typeCode.Type == typeof( uint ) && this._value < 0x80000000 )
						|| typeCode.Type == typeof( long )
						|| ( typeCode.Type == typeof( ulong ) && this._value < ( 0x80000000 << 32 ) );
				}
				case TypeCode.UInt64:
				{
					return
						( typeCode.Type == typeof( sbyte ) && this._value < 0x80 )
						|| typeCode.Type == typeof( byte )
						|| ( typeCode.Type == typeof( short ) && this._value < 0x8000 )
						|| typeCode.Type == typeof( ushort )
						|| ( typeCode.Type == typeof( int ) && this._value < 0x80000000 )
						|| typeCode.Type == typeof( uint )
						|| ( typeCode.Type == typeof( long ) && this._value < ( 0x80000000 << 32 ) )
						|| typeCode.Type == typeof( ulong );
				}
				case TypeCode.Double:
				{
					return
						typeCode.Type == typeof( float )
						|| typeCode.Type == typeof( double );
				}
			}

			return typeCode.Type == type;
		}

		/// <summary>
		///		Get the value indicates whether this instance wraps raw binary (or string) or not.
		/// </summary>
		/// <value>This instance wraps raw binary (or string) then true.</value>
		public bool IsRaw
		{
			get { return !this.IsNil && ( this._handleOrTypeCode is string || this._handleOrTypeCode is IEnumerable<byte> ); }
		}

		/// <summary>
		///		Get the value indicates whether this instance wraps list (array) or not.
		/// </summary>
		/// <value>This instance wraps list (array) then true.</value>
		public bool IsList
		{
			get { return !this.IsNil && this._handleOrTypeCode is IList<MessagePackObject>; }
		}

		/// <summary>
		///		Get the value indicates whether this instance wraps list (array) or not.
		/// </summary>
		/// <value>This instance wraps list (array) then true.</value>
		public bool IsArray
		{
			get { return this.IsList; }
		}

		/// <summary>
		///		Get the value indicates whether this instance wraps dictionary (map) or not.
		/// </summary>
		/// <value>This instance wraps dictionary (map) then true.</value>
		public bool IsDictionary
		{
			get { return !this.IsNil && this._handleOrTypeCode is IDictionary<MessagePackObject, MessagePackObject>; }
		}

		/// <summary>
		///		Get the value indicates whether this instance wraps dictionary (map) or not.
		/// </summary>
		/// <value>This instance wraps dictionary (map) then true.</value>
		public bool IsMap
		{
			get { return this.IsDictionary; }
		}

		/// <summary>
		///		Get underlying type of this instance.
		/// </summary>
		/// <returns>Underlying <see cref="Type"/>.</returns>
		public Type GetUnderlyingType()
		{
			if ( this._handleOrTypeCode == null )
			{
				return null;
			}

			var typeCode = this._handleOrTypeCode as ValueTypeCode;
			if ( typeCode == null )
			{
				var asMps = this._handleOrTypeCode as MessagePackString;
				if ( asMps != null )
				{
					return asMps.GetUnderlyingType();
				}
				else
				{
					return this._handleOrTypeCode.GetType();
				}
			}
			else
			{
				return typeCode.Type;
			}
		}

		#endregion -- Type Of Methods --

		/// <summary>
		///		Pack this instance itself using specified <see cref="Packer"/>.
		/// </summary>
		/// <param name="packer"><see cref="Packer"/>.</param>
		/// <param name="options">Packing options. This value can be null.</param>
		/// <exception cref="ArgumentNullException"><paramref name="packer"/> is null.</exception>
		public void PackToMessage( Packer packer, PackingOptions options )
		{
			if ( packer == null )
			{
				throw new ArgumentNullException( "packer" );
			}

			Contract.EndContractBlock();

			if ( this._handleOrTypeCode == null )
			{
				packer.PackNull();
				return;
			}

			var typeCode = this._handleOrTypeCode as ValueTypeCode;
			if ( typeCode == null )
			{
				packer.PackObject( this._handleOrTypeCode, options );
				return;
			}

			switch ( typeCode.TypeCode )
			{
				case TypeCode.Single:
				{
					if ( options == null || !options.IsStrict )
					{
						packer.Pack( ( float )this );
					}
					else
					{
						packer.PackStrict( ( float )this );
					}

					return;
				}
				case TypeCode.Double:
				{
					if ( options == null || !options.IsStrict )
					{
						packer.Pack( ( double )this );
					}
					else
					{
						packer.PackStrict( ( double )this );
					}

					return;
				}
				case TypeCode.SByte:
				{
					if ( options == null || !options.IsStrict )
					{
						packer.Pack( ( sbyte )this );
					}
					else
					{
						packer.PackStrict( ( sbyte )this );
					}

					return;
				}
				case TypeCode.Int16:
				{
					if ( options == null || !options.IsStrict )
					{
						packer.Pack( ( short )this );
					}
					else
					{
						packer.PackStrict( ( short )this );
					}

					return;
				}
				case TypeCode.Int32:
				{
					if ( options == null || !options.IsStrict )
					{
						packer.Pack( ( int )this );
					}
					else
					{
						packer.PackStrict( ( int )this );
					}

					return;
				}
				case TypeCode.Int64:
				{
					if ( options == null || !options.IsStrict )
					{
						packer.Pack( ( long )this );
					}
					else
					{
						packer.PackStrict( ( long )this );
					}

					return;
				}
				case TypeCode.Byte:
				{
					if ( options == null || !options.IsStrict )
					{
						packer.Pack( ( byte )this );
					}
					else
					{
						packer.PackStrict( ( byte )this );
					}

					return;
				}
				case TypeCode.UInt16:
				{
					if ( options == null || !options.IsStrict )
					{
						packer.Pack( ( ushort )this );
					}
					else
					{
						packer.PackStrict( ( ushort )this );
					}

					return;
				}
				case TypeCode.UInt32:
				{
					if ( options == null || !options.IsStrict )
					{
						packer.Pack( ( uint )this );
					}
					else
					{
						packer.PackStrict( ( uint )this );
					}

					return;
				}
				case TypeCode.UInt64:
				{
					if ( options == null || !options.IsStrict )
					{
						packer.Pack( this._value );
					}
					else
					{
						packer.PackStrict( this._value );
					}

					return;
				}
				case TypeCode.Boolean:
				{
					packer.Pack( this._value != 0 );
					return;
				}
				default:
				{
					Contract.Assume( false, "Unexpected type code :" + typeCode.TypeCode );
					packer.Pack( this._value, options );
					return;
				}
			}
		}

		#region -- Primitive Type Conversion Methods --

		/// <summary>
		///		Get underlying value as raw byte.
		/// </summary>
		/// <returns>Underlying raw binary.</returns>
		public byte[] AsBinary()
		{
			VerifyUnderlyingType<byte[]>( this, null );

			if ( this.IsNil )
			{
				return null;
			}

			return ( this._handleOrTypeCode as MessagePackString ).GetBytes();
		}

		/// <summary>
		///		Get underlying value as UTF8 string.
		/// </summary>
		/// <returns>Underlying raw binary.</returns>
		public string AsString()
		{
			return this.AsStringUtf8();
		}

		/// <summary>
		///		Get underlying value as UTF8 string.
		/// </summary>
		/// <returns>Underlying raw binary.</returns>
		public string AsString( Encoding encoding )
		{
			if ( encoding == null )
			{
				throw new ArgumentNullException( "encoding" );
			}

			// Short path to return just return not-encoded string.
			var asString = this._handleOrTypeCode as string;
			if ( asString != null )
			{
				return asString;
			}

			VerifyUnderlyingType<MessagePackString>( this, null );

			Contract.EndContractBlock();

			if ( this.IsNil )
			{
				return null;
			}

			try
			{
				var asBytes = this._handleOrTypeCode as MessagePackString;
				if ( asBytes.UnsafeGetBuffer() == null )
				{
					return null;
				}

				return encoding.GetString( asBytes.UnsafeGetBuffer(), 0, asBytes.UnsafeGetBuffer().Length );
			}
			catch ( ArgumentException ex )
			{
				throw new InvalidOperationException( String.Format( CultureInfo.CurrentCulture, "Not '{0}' string.", encoding.WebName ), ex );
			}
		}

		/// <summary>
		///		Get underlying value as UTF8 string.
		/// </summary>
		/// <returns>Underlying raw binary.</returns>
		public string AsStringUtf8()
		{
			return this.AsString( Encoding.UTF8 );
		}

		/// <summary>
		///		Get underlying value as UTF-16 string.
		/// </summary>
		/// <returns>Underlying string.</returns>
		/// <remarks>
		///		This method detects BOM. If BOM is not exist, them bytes should be Big-Endian UTF-16.
		/// </remarks>
		public string AsStringUtf16()
		{
			VerifyUnderlyingType<byte[]>( this, null );
			Contract.EndContractBlock();

			if ( this.IsNil )
			{
				return null;
			}

			try
			{
				var asBytes = this._handleOrTypeCode as byte[];
				if ( asBytes.Length == 0 )
				{
					return String.Empty;
				}

				if ( asBytes.Length % 2 != 0 )
				{
					throw new InvalidOperationException( "Not UTF-16 string." );
				}

				if ( asBytes[ 0 ] == 0xff && asBytes[ 1 ] == 0xfe )
				{
					return Encoding.Unicode.GetString( asBytes, 0, asBytes.Length );
				}
				else
				{
					return Encoding.BigEndianUnicode.GetString( asBytes, 0, asBytes.Length );
				}
			}
			catch ( ArgumentException ex )
			{
				throw new InvalidOperationException( "Not UTF-16 string.", ex );
			}
		}

		#endregion -- Primitive Type Conversion Methods --

		#region -- Container Type Conversion Methods --

		/// <summary>
		///		Get underlying value as <see cref="IEnumerable&lt;MessagePackObject&gt;"/>.
		/// </summary>
		/// <returns>Underlying <see cref="IEnumerable&lt;MessagePackObject&gt;"/>.</returns>
		public IEnumerable<MessagePackObject> AsEnumerable()
		{
			VerifyUnderlyingType<IList<MessagePackObject>>( this, null );
			if ( this.IsNil )
			{
				return null;
			}

			var asList = this._handleOrTypeCode as IList<MessagePackObject>;
			if ( asList != null )
			{
				return asList;
			}

			return ( this._handleOrTypeCode as MessagePackString ).GetBytes().Select( b => new MessagePackObject( b ) );
		}

		/// <summary>
		///		Get underlying value as <see cref="IList&lt;MessagePackObject&gt;"/>.
		/// </summary>
		/// <returns>Underlying <see cref="IList&lt;MessagePackObject&gt;"/>.</returns>
		public IList<MessagePackObject> AsList()
		{
			if ( this.IsNil )
			{
				return null;
			}

			return this.AsEnumerable().ToList();
		}

		/// <summary>
		///		Get underlying value as <see cref="IDictionary&lt;MessagePackObject, MessagePackObject&gt;"/>.
		/// </summary>
		/// <returns>Underlying <see cref="IDictionary&lt;MessagePackObject, MessagePackObject&gt;"/>.</returns>
		public IDictionary<MessagePackObject, MessagePackObject> AsDictionary()
		{
			VerifyUnderlyingType<IDictionary<MessagePackObject, MessagePackObject>>( this, null );

			if ( this.IsNil )
			{
				return null;
			}

			return this._handleOrTypeCode as IDictionary<MessagePackObject, MessagePackObject>;
		}

		#endregion -- Container Type Conversion Methods --

		#region -- Utility Methods --

		private static void VerifyUnderlyingType<T>( MessagePackObject instance, string parameterName )
		{
			if ( instance.IsNil )
			{
				// TODO: localize
				if ( parameterName != null )
				{
					throw new ArgumentException( String.Format( CultureInfo.CurrentCulture, "Do not convert nil MessagePackObject to {0}.", typeof( T ) ), parameterName );
				}
				else
				{
					throw new InvalidOperationException( String.Format( CultureInfo.CurrentCulture, "Do not convert nil MessagePackObject to {0}.", typeof( T ) ) );
				}
			}

			if ( !instance.IsTypeOf<T>().GetValueOrDefault() )
			{
				if ( parameterName != null )
				{
					throw new ArgumentException( String.Format( CultureInfo.CurrentCulture, "Do not convert {0} MessagePackObject to {1}.", instance.GetUnderlyingType(), typeof( T ) ), parameterName );
				}
				else
				{
					throw new InvalidOperationException( String.Format( CultureInfo.CurrentCulture, "Do not convert {0} MessagePackObject to {1}.", instance.GetUnderlyingType(), typeof( T ) ) );
				}
			}
		}

		#endregion -- Utility Methods --

		/// <summary>
		///		Wraps specified object as <see cref="MessagePackObject"/> recursively.
		/// </summary>
		/// <param name="boxedValue">Object to be wrapped.</param>
		/// <returns><see cref="MessagePackObject"/> wrapps <paramref name="boxedValue"/>.</returns>
		/// <exception cref="MessageTypeException">
		///		<paramref name="boxedValue"/> is not primitive value type, list of <see cref="MessagePackObject"/>,
		///		dictionary of <see cref="MessagePackObject"/>, <see cref="String"/>, <see cref="Byte"/>[], or null.
		/// </exception>
		public static MessagePackObject FromObject( object boxedValue )
		{
			return FromObject( boxedValue, ObjectPackingOptions.None /* ObjectPackingOptions.Recursive */ );
		}

		/// <summary>
		///		Wraps specified object as <see cref="MessagePackObject"/>.
		/// </summary>
		/// <param name="boxedValue">Object to be wrapped.</param>
		/// <param name="options">Wrapping options.</param>
		/// <returns><see cref="MessagePackObject"/> wrapps <paramref name="boxedValue"/>.</returns>
		/// <exception cref="MessageTypeException">
		///		<paramref name="boxedValue"/> is not primitive value type, list of <see cref="MessagePackObject"/>,
		///		dictionary of <see cref="MessagePackObject"/>, <see cref="String"/>, <see cref="Byte"/>[], or null.
		/// </exception>
		public static MessagePackObject FromObject( object boxedValue, ObjectPackingOptions options )
		{
			if ( boxedValue == null )
			{
				return MessagePackObject.Nil;
			}
			else if ( boxedValue is sbyte )
			{
				return ( sbyte )boxedValue;
			}
			else if ( boxedValue is sbyte? )
			{
				return ( sbyte? )boxedValue ?? MessagePackObject.Nil;
			}
			else if ( boxedValue is byte )
			{
				return ( byte )boxedValue;
			}
			else if ( boxedValue is byte? )
			{
				return ( byte? )boxedValue ?? MessagePackObject.Nil;
			}
			else if ( boxedValue is short )
			{
				return ( short )boxedValue;
			}
			else if ( boxedValue is short? )
			{
				return ( short? )boxedValue ?? MessagePackObject.Nil;
			}
			else if ( boxedValue is ushort )
			{
				return ( ushort )boxedValue;
			}
			else if ( boxedValue is ushort? )
			{
				return ( ushort? )boxedValue ?? MessagePackObject.Nil;
			}
			else if ( boxedValue is int )
			{
				return ( int )boxedValue;
			}
			else if ( boxedValue is int? )
			{
				return ( int? )boxedValue ?? MessagePackObject.Nil;
			}
			else if ( boxedValue is uint )
			{
				return ( uint )boxedValue;
			}
			else if ( boxedValue is uint? )
			{
				return ( uint? )boxedValue ?? MessagePackObject.Nil;
			}
			else if ( boxedValue is long )
			{
				return ( long )boxedValue;
			}
			else if ( boxedValue is long? )
			{
				return ( long? )boxedValue ?? MessagePackObject.Nil;
			}
			else if ( boxedValue is ulong )
			{
				return ( ulong )boxedValue;
			}
			else if ( boxedValue is ulong? )
			{
				return ( ulong? )boxedValue ?? MessagePackObject.Nil;
			}
			else if ( boxedValue is float )
			{
				return ( float )boxedValue;
			}
			else if ( boxedValue is float? )
			{
				return ( float? )boxedValue ?? MessagePackObject.Nil;
			}
			else if ( boxedValue is double )
			{
				return ( double )boxedValue;
			}
			else if ( boxedValue is double? )
			{
				return ( double? )boxedValue ?? MessagePackObject.Nil;
			}
			else if ( boxedValue is bool )
			{
				return ( bool )boxedValue;
			}
			else if ( boxedValue is bool? )
			{
				return ( bool? )boxedValue ?? MessagePackObject.Nil;
			}
			else if ( boxedValue is byte[] )
			{
				return new MessagePackObject( boxedValue as byte[] );
			}
			else if ( boxedValue is string )
			{
				return new MessagePackObject( boxedValue as string );
			}
			else if ( boxedValue is IEnumerable<byte> )
			{
				return new MessagePackObject( ( boxedValue as IEnumerable<byte> ).ToArray() );
			}
			else if ( boxedValue is IEnumerable<char> )
			{
				return new MessagePackObject( new String( ( boxedValue as IEnumerable<char> ).ToArray() ) );
			}
			else if ( boxedValue is IEnumerable<MessagePackObject> )
			{
				var asList = boxedValue as IList<MessagePackObject>;
				if ( asList != null )
				{
					return new MessagePackObject( asList );
				}
				else
				{
					return new MessagePackObject( ( boxedValue as IEnumerable<MessagePackObject> ).ToList() );
				}
			}
			else if ( boxedValue is IDictionary<MessagePackObject, MessagePackObject> )
			{
				return new MessagePackObject( boxedValue as IDictionary<MessagePackObject, MessagePackObject> );
			}

			/*
			if ( options.Has( ObjectPackingOptions.Recursive ) )
			{
				var types = boxedValue.GetType().GetInterfaces();
				var dictionaryType =
					types
					.Where( type => type.IsGenericType )
					.Select( type => new KeyValuePair<Type, Type>( type, type.GetGenericTypeDefinition() ) )
					.FirstOrDefault( tuple => tuple.Value == typeof( IDictionary<,> ) )
					.Key;
				if ( dictionaryType != null )
				{
					try
					{
						dynamic asDynamic = boxedValue;
						// To dispatch Pack<TKey,TValue>(IDictionary<TKey,TValue>) effeciently, use dynamic infrastructure.
						return new MessagePackObject( asDynamic );
					}
					catch ( RuntimeBinderException )
					{
						throw new MessageTypeException( String.Format( CultureInfo.CurrentCulture, "Dictionary (map) type '{0}' is not supported.", boxedValue.GetType() ) );
					}
				}

				var listType =
					types
					.Where( type => type.IsGenericType )
					.Select( type => new KeyValuePair<Type, Type>( type, type.GetGenericTypeDefinition() ) )
					.FirstOrDefault( tuple => tuple.Value == typeof( IEnumerable<> ) )
					.Key;

				if ( listType != null )
				{
					try
					{
						dynamic asDynamic = boxedValue;
						// To dispatch Pack<T>(IEnumerable<T>) effeciently, use dynamic infrastructure.
						return new MessagePackObject( asDynamic );
					}
					catch ( RuntimeBinderException )
					{
						throw new MessageTypeException( String.Format( CultureInfo.CurrentCulture, "List (array) type '{0}' is not supported.", boxedValue.GetType() ) );
					}
				}
			}
			 */

			throw new MessageTypeException( String.Format( CultureInfo.CurrentCulture, "Type '{0}' is not supported.", boxedValue.GetType() ) );
		}

		/// <summary>
		///		Get boxed underlying value for this object.
		/// </summary>
		/// <returns>Boxed underlying value for this object.</returns>
		public object ToObject()
		{
			if ( this._handleOrTypeCode == null )
			{
				return null;
			}

			var asType = this._handleOrTypeCode as ValueTypeCode;
			if ( asType == null )
			{
				var asBinary = this._handleOrTypeCode as MessagePackString;
				if ( asBinary != null )
				{
					var asString = asBinary.TryGetString();
					if ( asString != null )
					{
						return asString;
					}

					return asBinary.UnsafeGetBuffer();
				}

				var asDictionary = this._handleOrTypeCode as IDictionary<MessagePackObject, MessagePackObject>;
				if ( asDictionary != null )
				{
					return asDictionary;
				}

				var asList = this._handleOrTypeCode as IList<MessagePackObject>;
				if ( asList != null )
				{
					return asList;
				}

				Contract.Assert( false, "Unknwon type:" + this._handleOrTypeCode );
				return null;
			}
			else
			{
				switch ( asType.TypeCode )
				{
					case TypeCode.Boolean:
					{
						return this.AsBoolean();
					}
					case TypeCode.Byte:
					{
						return this.AsByte();
					}
					case TypeCode.Int16:
					{
						return this.AsInt16();
					}
					case TypeCode.Int32:
					{
						return this.AsInt32();
					}
					case TypeCode.Int64:
					{
						return this.AsInt64();
					}
					case TypeCode.SByte:
					{
						return this.AsSByte();
					}
					case TypeCode.UInt16:
					{
						return this.AsUInt16();
					}
					case TypeCode.UInt32:
					{
						return this.AsUInt32();
					}
					case TypeCode.UInt64:
					{
						return this.AsUInt64();
					}
					case TypeCode.Single:
					{
						return this.AsSingle();
					}
					case TypeCode.Double:
					{
						return this.AsDouble();
					}
					default:
					{
						Contract.Assert( false, "Unknwon type code:" + asType.TypeCode );
						return null;
					}
				}
			}
		}

		#region -- Structure Operator Overloads --

		/// <summary>
		///		Compare two instances are equal.
		/// </summary>
		/// <param name="left"><see cref="MessagePackObject"/> instance.</param>
		/// <param name="right"><see cref="MessagePackObject"/> instance.</param>
		/// <returns>
		///		Whether value of <paramref name="left"/> and <paramref name="right"/> are equal each other or not.
		/// </returns>
		public static bool operator ==( MessagePackObject left, MessagePackObject right )
		{
			return left.Equals( right );
		}

		/// <summary>
		///		Compare two instances are not equal.
		/// </summary>
		/// <param name="left"><see cref="MessagePackObject"/> instance.</param>
		/// <param name="right"><see cref="MessagePackObject"/> instance.</param>
		/// <returns>
		///		Whether value of <paramref name="left"/> and <paramref name="right"/> are not equal each other or are equal.
		/// </returns>
		public static bool operator !=( MessagePackObject left, MessagePackObject right )
		{
			return !left.Equals( right );
		}

		#endregion -- Structure Operator Overloads --


		#region -- Conversion Operator Overloads --

		/// <summary>
		///		Convert raw byte array to <see cref="MessagePackObject"/> instance.
		/// </summary>
		/// <param name="value">Raw byte array.</param>
		/// <returns><see cref="MessagePackObject"/> instance corresponds to <paramref name="value"/>.</returns>
		public static implicit operator MessagePackObject( byte[] value )
		{
			return new MessagePackObject( value );
		}

		/// <summary>
		///		Convert string to <see cref="MessagePackObject"/> instance using UTF-8 encoding.
		/// </summary>
		/// <param name="value">String.</param>
		/// <returns><see cref="MessagePackObject"/> instance corresponds to <paramref name="value"/>.</returns>
		public static implicit operator MessagePackObject( string value )
		{
			return new MessagePackObject( value );
		}

		/// <summary>
		///		Convert <see cref="MessagePackObject"/>[] instance to <see cref="MessagePackObject"/> instance.
		/// </summary>
		/// <param name="value"><see cref="MessagePackObject"/>[] instance.</param>
		/// <returns><see cref="MessagePackObject"/> instance corresponds to <paramref name="value"/>.</returns>
		public static implicit operator MessagePackObject( MessagePackObject[] value )
		{
			return new MessagePackObject( value );
		}

		/// <summary>
		///		Convert this instance to byte array.
		/// </summary>
		/// <param name="value"><see cref="MessagePackObject"/> instance.</param>
		/// <returns>Raw byte array of <paramref name="value"/>.</returns>
		public static explicit operator byte[]( MessagePackObject value )
		{
			VerifyUnderlyingType<MessagePackString>( value, "value" );

			return ( value._handleOrTypeCode as MessagePackString ).GetBytes();
		}

		/// <summary>
		///		Convert this instance to UTF-8 string.
		/// </summary>
		/// <param name="value"><see cref="MessagePackObject"/> instance.</param>
		/// <returns>Raw byte array of <paramref name="value"/>.</returns>
		public static explicit operator string( MessagePackObject value )
		{
			var asBytes = ( byte[] )value;
			return Encoding.UTF8.GetString( asBytes, 0, asBytes.Length );
		}

		#endregion -- Conversion Operator Overloads --

		private sealed class ValueTypeCode
		{
			private readonly TypeCode _typeCode;

			public TypeCode TypeCode
			{
				get { return this._typeCode; }
			}

			private readonly Type _type;

			public Type Type
			{
				get { return this._type; }
			}

			internal ValueTypeCode( Type type )
			{
				this._type = type;
				this._typeCode = Type.GetTypeCode( type );
			}

			public override string ToString()
			{
				// For debuggability.
				return this._typeCode == System.TypeCode.Object ? this._type.FullName : this._typeCode.ToString();
			}
		}
	}
}