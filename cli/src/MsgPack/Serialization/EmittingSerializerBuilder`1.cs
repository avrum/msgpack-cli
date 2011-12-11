#region -- License Terms --
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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using NLiblet.Reflection;

namespace MsgPack.Serialization
{
	// TODO: Move fully-non-generic members to non-generic helpers to reduce JIT
	/// <summary>
	///		<see cref="SerializerBuilder{T}"/> implementation using Reflection.Emit.
	/// </summary>
	/// <typeparam name="TObject">Object to be serialized/deserialized.</typeparam>
	internal sealed class EmittingSerializerBuilder<TObject> : SerializerBuilder<TObject>
	{
		public EmittingSerializerBuilder( SerializationContext context ) : base( context ) { }

		protected sealed override MessagePackSerializer<TObject> CreateSerializer( SerializingMember[] entries )
		{
			var emitter = SerializationMethodGeneratorManager.Get().CreateEmitter( typeof( TObject ) );

			var packerIL = emitter.GetPackToMethodILGenerator();
			try
			{
				Emittion.EmitPackMambers(
					emitter,
					packerIL,
					1,
					typeof( TObject ),
					2,
					entries.Select( item => Tuple.Create( item.Member, item.Member.GetMemberValueType() ) ).ToArray()
					);
				packerIL.EmitRet();
			}
			finally
			{
				packerIL.FlushTrace();
			}

			var unpackerIL = emitter.GetUnpackFromMethodILGenerator();
			try
			{
				// TODO: Array for ordered.
				// TODO: For big struct, use Dictionary<String,SM>
				// TODO: Required
				var locals = entries.Select( item => item.Member.CanSetValue() ? unpackerIL.DeclareLocal( item.Member.GetMemberValueType(), item.Contract.Name ) : null ).ToArray();

				var result = unpackerIL.DeclareLocal( typeof( TObject ), "result" );
				Emittion.EmitConstruction( unpackerIL, result, null );

				Emittion.EmitUnpackMembers(
					emitter,
					unpackerIL,
					1,
					result,
					entries.Zip(
						locals,
						( entry, local ) => new Tuple<MemberInfo, string, LocalBuilder, LocalBuilder>( entry.Member, entry.Contract.Name, local, default( LocalBuilder ) )
					).ToArray()
				);

				foreach ( var item in entries.Zip( locals, ( Entry, Local ) => new { Entry, Local } ) )
				{
					if ( item.Local == null )
					{
						continue;
					}

					if ( result.LocalType.IsValueType )
					{
						unpackerIL.EmitAnyLdloca( result );
					}
					else
					{
						unpackerIL.EmitAnyLdloc( result );
					}

					unpackerIL.EmitAnyLdloc( item.Local );
					Emittion.EmitStoreValue( unpackerIL, item.Entry.Member );
				}

				unpackerIL.EmitAnyLdloc( result );
				unpackerIL.EmitRet();
			}
			finally
			{
				unpackerIL.FlushTrace();
			}

			return emitter.CreateInstance<TObject>( this.Context );
		}

		public sealed override MessagePackSerializer<TObject> CreateArraySerializer()
		{
			return CreateArraySerializerCore( typeof( TObject ), null ).CreateInstance<TObject>( this.Context );
		}

		protected sealed override ConstructorInfo CreateArraySerializer( MemberInfo member, Type memberType )
		{
			return CreateArraySerializerCore( memberType, member ).Create();
		}

		private static SerializerEmitter CreateArraySerializerCore( Type collectionType, MemberInfo memberOrNull )
		{
			var emitter = SerializationMethodGeneratorManager.Get().CreateEmitter( collectionType );
			CreatePackArrayProceduresCore( emitter, memberOrNull );
			CreateUnpackArrayProceduresCore( emitter, memberOrNull, collectionType );
			return emitter;
		}

		private static void CreatePackArrayProceduresCore( SerializerEmitter emitter, MemberInfo memberOrNull )
		{
			var traits = typeof( TObject ).GetCollectionTraits();
			var il = emitter.GetPackToMethodILGenerator();
			try
			{
				// Array
				if ( typeof( TObject ).IsArray )
				{
					/*
					 * // array
					 *  packer.PackArrayHeader( length );
					 * for( int i = 0; i < length; i++ )
					 * {
					 * 		this._serializer.PackTo( packer, collection[ i ] );
					 * }
					 */
					var length = il.DeclareLocal( typeof( int ), "length" );
					il.EmitAnyLdarg( 2 );
					if ( memberOrNull != null )
					{
						Emittion.EmitLoadValue( il, memberOrNull );
					}
					il.EmitLdlen();
					il.EmitAnyStloc( length );
					il.EmitAnyLdarg( 1 );
					il.EmitAnyLdloc( length );
					il.EmitAnyCall( Metadata._Packer.PackArrayHeader );
					il.EmitPop();
					Emittion.EmitFor(
						il,
						length,
						( il0, i ) =>
						{
							Emittion.EmitMarshalValue(
								emitter,
								il0,
								1,
								traits.ElementType,
								il1 =>
								{
									il1.EmitAnyLdarg( 2 );
									if ( memberOrNull != null )
									{
										Emittion.EmitLoadValue( il1, memberOrNull );
									}
									il1.EmitAnyLdloc( i );
									il1.EmitLdelem( traits.ElementType );
								}
							);
						}
					);
				}
				else if ( traits.CountProperty == null )
				{
					/*
					 *  array = collection.ToArray();
					 *  packer.PackArrayHeader( length );
					 * for( int i = 0; i < length; i++ )
					 * {
					 * 		this._serializer.PackTo( packer, array[ i ] );
					 * }
					 */
					var array = il.DeclareLocal( traits.ElementType.MakeArrayType(), "array" );
					il.EmitAnyLdarg( 2 );
					if ( memberOrNull != null )
					{
						Emittion.EmitLoadValue( il, memberOrNull );
					}
					il.EmitAnyCall( Metadata._Enumerable.ToArray1Method.MakeGenericMethod( traits.ElementType ) );
					il.EmitAnyStloc( array );
					var length = il.DeclareLocal( typeof( int ), "length" );
					il.EmitAnyLdloc( array );
					il.EmitLdlen();
					il.EmitAnyStloc( length );
					il.EmitAnyLdarg( 1 );
					il.EmitAnyLdloc( length );
					il.EmitAnyCall( Metadata._Packer.PackArrayHeader );
					il.EmitPop();
					Emittion.EmitFor(
						il,
						length,
						( il0, i ) =>
						{
							Emittion.EmitMarshalValue(
								emitter,
								il0,
								1,
								traits.ElementType,
								il1 =>
								{
									il1.EmitAnyLdloc( array );
									il1.EmitAnyLdloc( i );
									il1.EmitLdelem( traits.ElementType );
								}
							);
						}
					);
				}
				else
				{
					/*
					 * // Enumerable
					 *  packer.PackArrayHeader( collection.Count );
					 * foreach( var item in list )
					 * {
					 * 		this._serializer.PackTo( packer, array[ i ] );
					 * }
					 */
					var collection = il.DeclareLocal( typeof( TObject ), "collection" );
					il.EmitAnyLdarg( 2 );
					if ( memberOrNull != null )
					{
						Emittion.EmitLoadValue( il, memberOrNull );
					}
					il.EmitAnyStloc( collection );
					var count = il.DeclareLocal( typeof( int ), "count" );
					il.EmitAnyLdarg( 2 );
					if ( memberOrNull != null )
					{
						Emittion.EmitLoadValue( il, memberOrNull );
					}
					il.EmitGetProperty( traits.CountProperty );
					il.EmitAnyStloc( count );
					il.EmitAnyLdarg( 1 );
					il.EmitAnyLdloc( count );
					il.EmitAnyCall( Metadata._Packer.PackArrayHeader );
					il.EmitPop();
					Emittion.EmitForEach(
						il,
						traits,
						collection,
						( il0, getCurrentEmitter ) =>
						{
							Emittion.EmitMarshalValue(
								emitter,
								il0,
								1,
								traits.ElementType,
								_ => getCurrentEmitter()
							);
						}
					);
				}
				il.EmitRet();
			}
			finally
			{
				il.FlushTrace();
			}
		}

		private static void CreateUnpackArrayProceduresCore( SerializerEmitter emitter, MemberInfo memberOrNull, Type collectionType )
		{
			var traits = collectionType.GetCollectionTraits();
			CreateArrayUnpackFrom( emitter, collectionType );
			CreateArrayUnpackTo( emitter, memberOrNull, collectionType, traits );
		}

		private static void CreateArrayUnpackFrom( SerializerEmitter emitter, Type collectionType )
		{
			var unpackFromIL = emitter.GetUnpackFromMethodILGenerator();
			try
			{
				if ( collectionType.IsInterface || collectionType.IsAbstract )
				{
					unpackFromIL.EmitTypeOf( collectionType );
					unpackFromIL.EmitAnyCall( SerializationExceptions.NewNotSupportedBecauseCannotInstanciateAbstractTypeMethod );
					unpackFromIL.EmitThrow();
					return;
				}

				/*
				 *	if (!unpacker.IsArrayHeader)
				 *	{
				 *		throw SerializationExceptions.NewIsNotArrayHeader();
				 *	}
				 *	
				 *	TCollection collection = new ...;
				 *	this.UnpackToCore(unpacker, array);
				 *	return collection;
				 */

				unpackFromIL.EmitAnyLdarg( 1 );
				unpackFromIL.EmitGetProperty( Metadata._Unpacker.IsArrayHeader );
				var endIf = unpackFromIL.DefineLabel( "END_IF" );
				unpackFromIL.EmitBrtrue_S( endIf );
				unpackFromIL.EmitAnyCall( SerializationExceptions.NewIsNotArrayHeaderMethod );
				unpackFromIL.EmitThrow();
				unpackFromIL.MarkLabel( endIf );
				var collection = unpackFromIL.DeclareLocal( collectionType, "collection" );
				// Emit newobj, newarr, or call ValueType..ctor()
				Emittion.EmitConstruction(
					unpackFromIL,
					collection,
					il =>
					{
						il.EmitAnyLdarg( 1 );
						il.EmitGetProperty( Metadata._Unpacker.ItemsCount );
						il.EmitConv_Ovf_I4();
					}
				);

				unpackFromIL.EmitAnyLdarg( 0 );
				unpackFromIL.EmitAnyLdarg( 1 );
				unpackFromIL.EmitAnyLdloc( collection );
				unpackFromIL.EmitAnyCall( MessagePackSerializer<TObject>.UnpackToCoreMethod );
				unpackFromIL.EmitAnyLdloc( collection );
				unpackFromIL.EmitRet();
			}
			finally
			{
				unpackFromIL.FlushTrace();
			}
		}

		private static void CreateArrayUnpackTo( SerializerEmitter emitter, MemberInfo memberOrNull, Type collectionType, CollectionTraits traits )
		{
			/*
			 *	int count = checked((int)unpacker.ItemsCount);
			 *	for (int i = 0; i < count; i++)
			 *	{
			 *		if (!unpacker.Read())
			 *		{
			 *			throw SerializationExceptions.NewMissingItem(i);
			 *		}
			 *		
			 *		T item;
			 *		if (!unpacker.IsArrayHeader && !unpacker.IsMapHeader)
			 *		{
			 *			item = this._serializer.UnpackFrom(unpacker);
			 *		}
			 *		else
			 *		{
			 *			using (Unpacker subTreeUnpacker = unpacker.ReadSubtree())
			 *			{
			 *				item = this._serializer.UnpackFrom(subTreeUnpacker);
			 *			}
			 *		}
			 *		
			 *		collection[i] = guid;
			 *	}
	}		 */

			var il = emitter.GetUnpackToMethodILGenerator();
			try
			{
				var itemsCount = il.DeclareLocal( typeof( int ), "itemsCount" );

				il.EmitAnyLdarg( 1 );
				il.EmitGetProperty( Metadata._Unpacker.ItemsCount );
				il.EmitConv_Ovf_I4();
				il.EmitAnyStloc( itemsCount );
				Emittion.EmitFor(
					il,
					itemsCount,
					( il0, i ) =>
					{
						var value = il0.DeclareLocal( traits.ElementType, "value" );
						Emittion.EmitUnmarshalValue(
							emitter,
							il0,
							1,
							value,
							( il1, unpacker ) =>
							{
								il1.EmitAnyLdarg( unpacker );
								il1.EmitAnyCall( Metadata._Unpacker.Read );
								var endIf = il1.DefineLabel( "END_IF" );
								il1.EmitBrtrue_S( endIf );
								il1.EmitAnyLdloc( i );
								il1.EmitAnyCall( SerializationExceptions.NewMissingItemMethod );
								il1.EmitThrow();
								il1.MarkLabel( endIf );
							}
						);

						if ( memberOrNull != null )
						{
							Emittion.EmitLoadValue( il0, memberOrNull );
						}
						else
						{
							il0.EmitAnyLdarg( 2 );
						}

						if ( collectionType.IsArray )
						{
							il0.EmitAnyLdloc( i );
						}

						il0.EmitAnyLdloc( value );

						if ( collectionType.IsArray )
						{
							il0.EmitStelem( traits.ElementType );
						}
						else
						{
							il0.EmitAnyCall( traits.AddMethod );
							if ( traits.AddMethod.ReturnType != typeof( void ) )
							{
								il0.EmitPop();
							}
						}
					}
				);
				il.EmitRet();
			}
			finally
			{
				il.FlushTrace();
			}
		}

		public sealed override MessagePackSerializer<TObject> CreateMapSerializer()
		{
			return CreateMapSerializerCore( typeof( TObject ), null ).CreateInstance<TObject>( this.Context );
		}

		protected sealed override ConstructorInfo CreateMapSerializer( MemberInfo member, Type memberType )
		{
			return CreateMapSerializerCore( memberType, member ).Create();
		}

		private static SerializerEmitter CreateMapSerializerCore( Type collectionType, MemberInfo memberOrNull )
		{
			var emitter = SerializationMethodGeneratorManager.Get().CreateEmitter( collectionType );
			var traits = collectionType.GetCollectionTraits();
			CreateMapPack(
				emitter,
				collectionType,
				traits,
				( il, collection ) =>
				{
					il.EmitAnyLdarg( 2 );
					if ( memberOrNull != null )
					{
						Emittion.EmitLoadValue( il, memberOrNull );
					}

					il.EmitAnyStloc( collection );
				}
			);
			CreateMapUnpack(
				emitter,
				memberOrNull,
				collectionType,
				traits
			);

			return emitter;
		}

		private static void CreateMapPack( SerializerEmitter emiter, Type collectionType, CollectionTraits traits, Action<TracingILGenerator, LocalBuilder> loadCollectionEmitter )
		{
			var il = emiter.GetPackToMethodILGenerator();
			try
			{

				/*
				 * 	int count = ((ICollection<KeyValuePair<string, DateTime>>)dictionary).Count;
				 * 	packer.PackMapHeader(count);
				 * 	foreach (KeyValuePair<string, DateTime> current in dictionary)
				 * 	{
				 * 		this._serializer0.PackTo(packer, current.Key);
				 * 		this._serializer1.PackTo(packer, current.Value);
				 * 	}
				 */

				var collection = il.DeclareLocal( collectionType, "collection" );
				var item = il.DeclareLocal( traits.ElementType, "item" );
				var keyProperty = traits.ElementType.GetProperty( "Key" );
				var valueProperty = traits.ElementType.GetProperty( "Value" );
				loadCollectionEmitter( il, collection );
				var count = il.DeclareLocal( typeof( int ), "count" );
				il.EmitAnyLdloc( collection );
				il.EmitGetProperty( traits.CountProperty );
				il.EmitAnyStloc( count );
				il.EmitAnyLdarg( 1 );
				il.EmitAnyLdloc( count );
				il.EmitAnyCall( Metadata._Packer.PackMapHeader );
				il.EmitPop();

				Emittion.EmitForEach(
					il,
					traits,
					collection,
					( il0, getCurrentEmitter ) =>
					{
						if ( traits.ElementType.IsGenericType )
						{
							Contract.Assert( traits.ElementType.GetGenericTypeDefinition() == typeof( KeyValuePair<,> ) );
							getCurrentEmitter();
							il0.EmitAnyStloc( item );
							Emittion.EmitMarshalValue(
								emiter,
								il0,
								1,
								traits.ElementType.GetGenericArguments()[ 0 ],
								il1 =>
								{
									il1.EmitAnyLdloca( item );
									il1.EmitGetProperty( keyProperty );
								}
							);

							Emittion.EmitMarshalValue(
								emiter,
								il0,
								1,
								traits.ElementType.GetGenericArguments()[ 1 ],
								il1 =>
								{
									il1.EmitAnyLdloca( item );
									il1.EmitGetProperty( valueProperty );
								}
							);
						}
						else
						{
							Contract.Assert( traits.ElementType == typeof( DictionaryEntry ) );
							getCurrentEmitter();
							il0.EmitAnyStloc( item );
							Emittion.EmitMarshalValue(
								emiter,
								il0,
								1,
								typeof( MessagePackObject ),
								il1 =>
								{
									il0.EmitAnyLdloca( item );
									il0.EmitGetProperty( Metadata._DictionaryEntry.Key );
									il0.EmitUnbox_Any( typeof( MessagePackObject ) );
								}
							);

							Emittion.EmitMarshalValue(
								emiter,
								il0,
								1,
								typeof( MessagePackObject ),
								il1 =>
								{
									il0.EmitAnyLdloca( item );
									il0.EmitGetProperty( Metadata._DictionaryEntry.Value );
									il0.EmitUnbox_Any( typeof( MessagePackObject ) );
								}
							);
						}
					}
				);
				il.EmitRet();
			}
			finally
			{
				il.FlushTrace();
			}
		}

		private static void CreateMapUnpack( SerializerEmitter emitter, MemberInfo memberOrNull, Type collectionType, CollectionTraits traits )
		{
			CreateMapUnpackFrom( emitter, memberOrNull, collectionType );
			CreateMapUnpackTo( emitter, traits );
		}

		private static void CreateMapUnpackFrom( SerializerEmitter emitter, MemberInfo memberOrNull, Type collectionType )
		{
			var il = emitter.GetUnpackFromMethodILGenerator();
			try
			{
				/*
				 *	if (!unpacker.IsMapHeader)
				 *	{
				 *		throw SerializationExceptions.NewIsNotMapHeader();
				 *	}
				 *	
				 *	TDictionary<TKey, TValue> dictionary = new ...;
				 *	this.UnpackToCore(unpacker, dictionary);
				 *	return dictionary;
				 */

				if ( collectionType.IsInterface || collectionType.IsAbstract )
				{
					il.EmitTypeOf( collectionType );
					il.EmitAnyCall( SerializationExceptions.NewNotSupportedBecauseCannotInstanciateAbstractTypeMethod );
					il.EmitThrow();
					return;
				}

				il.EmitAnyLdarg( 1 );
				il.EmitGetProperty( Metadata._Unpacker.IsMapHeader );
				var endIf = il.DefineLabel( "END_IF" );
				il.EmitBrtrue_S( endIf );
				il.EmitAnyCall( SerializationExceptions.NewIsNotMapHeaderMethod );
				il.EmitThrow();
				il.MarkLabel( endIf );

				var collection = il.DeclareLocal( collectionType, "collection" );
				if ( memberOrNull == null )
				{
					Emittion.EmitConstruction(
						il,
						collection,
						il0 =>
						{
							il0.EmitAnyLdarg( 1 );
							il0.EmitGetProperty( Metadata._Unpacker.ItemsCount );
							il0.EmitConv_Ovf_I4();
						}
					);
				}
				else
				{
					il.EmitAnyLdarg( 2 );
					Emittion.EmitLoadValue( il, memberOrNull );
					il.EmitAnyStloc( collection );
				}

				il.EmitAnyLdarg( 0 );
				il.EmitAnyLdarg( 1 );
				il.EmitAnyLdloc( collection );
				il.EmitAnyCall( MessagePackSerializer<TObject>.UnpackToCoreMethod );
				il.EmitAnyLdloc( collection );
				il.EmitRet();
			}
			finally
			{
				il.FlushTrace();
			}
		}

		private static void CreateMapUnpackTo( SerializerEmitter emitter, CollectionTraits traits )
		{
			var il = emitter.GetUnpackToMethodILGenerator();
			try
			{
				/*
				 *	if (!unpacker.IsMapHeader)
				 *	{
				 *		throw SerializationExceptions.NewIsNotMapHeader();
				 *	}
				 *	
				 *	int count = checked((int)unpacker.ItemsCount);
				 *	for (int i = 0; i < count; i++)
				 *	{
				 *		if (!unpacker.Read())
				 *		{
				 *			throw SerializationExceptions.NewMissingItem(i);
				 *		}
				 *		
				 *		TKey key;
				 *		if (!unpacker.IsArrayHeader && !unpacker.IsMapHeader)
				 *		{
				 *			key = this._serializer0.UnpackFrom(unpacker);
				 *		}
				 *		else
				 *		{
				 *			using (Unpacker subTreeUnpacker = unpacker.ReadSubtree())
				 *			{
				 *				key = this._serializer0.UnpackFrom(subTreeUnpacker);
				 *			}
				 *		}
				 *		
				 *		if (!unpacker.Read())
				 *		{
				 *			throw SerializationExceptions.NewMissingItem(i);
				 *		}
				 *		
				 *		TValue value;
				 *		if (!unpacker.IsArrayHeader && !unpacker.IsMapHeader)
				 *		{
				 *			value = this._serializer1.UnpackFrom(unpacker);
				 *		}
				 *		else
				 *		{
				 *			using (Unpacker subTreeUnpacker = unpacker.ReadSubtree())
				 *			{
				 *				value = this._serializer1.UnpackFrom(subTreeUnpacker);
				 *			}
				 *		}
				 *		
				 *		dictionary.Add(key, value);
				 *	}
				 */

				il.EmitAnyLdarg( 1 );
				il.EmitGetProperty( Metadata._Unpacker.IsMapHeader );
				var endIf = il.DefineLabel( "END_IF" );
				il.EmitBrtrue_S( endIf );
				il.EmitAnyCall( SerializationExceptions.NewIsNotMapHeaderMethod );
				il.EmitThrow();
				il.MarkLabel( endIf );

				var itemsCount = il.DeclareLocal( typeof( int ), "itemsCount" );
#if DEBUG
				Contract.Assert( traits.ElementType.IsGenericType && traits.ElementType.GetGenericTypeDefinition() == typeof( KeyValuePair<,> )
					|| traits.ElementType == typeof( DictionaryEntry ) );
#endif

				var key = il.DeclareLocal( traits.ElementType.IsGenericType ? traits.ElementType.GetGenericArguments()[ 0 ] : typeof( MessagePackObject ), "key" );
				var value = il.DeclareLocal( traits.ElementType.IsGenericType ? traits.ElementType.GetGenericArguments()[ 1 ] : typeof( MessagePackObject ), "value" );

				il.EmitAnyLdarg( 1 );
				il.EmitGetProperty( Metadata._Unpacker.ItemsCount );
				il.EmitConv_Ovf_I4();
				il.EmitAnyStloc( itemsCount );
				Emittion.EmitFor(
					il,
					itemsCount,
					( il0, i ) =>
					{
						Action<TracingILGenerator, int> unpackerReading =
							( il1, unpacker ) =>
							{
								il1.EmitAnyLdarg( unpacker );
								il1.EmitAnyCall( Metadata._Unpacker.Read );
								var endIf0 = il1.DefineLabel( "END_IF0" );
								il1.EmitBrtrue_S( endIf0 );
								il1.EmitAnyLdloc( i );
								il1.EmitAnyCall( SerializationExceptions.NewMissingItemMethod );
								il1.EmitThrow();
								il1.MarkLabel( endIf0 );
							};

						// Key
						Emittion.EmitUnmarshalValue( emitter, il0, 1, key, unpackerReading );

						// Value
						Emittion.EmitUnmarshalValue( emitter, il0, 1, value, unpackerReading );

						il0.EmitAnyLdarg( 2 );

						il0.EmitAnyLdloc( key );
						if ( !traits.ElementType.IsGenericType )
						{
							il0.EmitBox( key.LocalType );
						}

						il0.EmitAnyLdloc( value );
						if ( !traits.ElementType.IsGenericType )
						{
							il0.EmitBox( value.LocalType );
						}

						il0.EmitAnyCall( traits.AddMethod );
						if ( traits.AddMethod.ReturnType != typeof( void ) )
						{
							il0.EmitPop();
						}
					}
				);
				il.EmitRet();
			}
			finally
			{
				il.FlushTrace();
			}
		}

		protected sealed override ConstructorInfo CreateObjectSerializer( MemberInfo member, Type memberType )
		{
			var emitter = SerializationMethodGeneratorManager.Get().CreateEmitter( memberType );
			CreateObjectPack( emitter, member, memberType );
			CreateObjectUnpack( emitter, member, memberType );
			return emitter.Create();
		}

		private static void CreateObjectPack( SerializerEmitter emitter, MemberInfo member, Type memberType )
		{
			/*
			 * Context.Serializers.Get<T>().Serialize( packer, value.... );
			 */
			var il = emitter.GetPackToMethodILGenerator();
			try
			{
				Emittion.EmitMarshalValue(
					emitter,
					il,
					1,
					memberType,
					il0 =>
					{
						il0.EmitAnyLdarg( 2 );
						Emittion.EmitLoadValue( il0, member );
					}
				);
				il.EmitRet();
			}
			finally
			{
				il.FlushTrace();
			}
		}

		private static void CreateObjectUnpack( SerializerEmitter emitter, MemberInfo member, Type memberType )
		{
			/*
			 * Context.Serializers.Get<T>().Deserialize( packer, value.... );
			 */
			var il = emitter.GetUnpackFromMethodILGenerator();
			try
			{
				var itemsCount = il.DeclareLocal( typeof( int ), "itemsCount" );
				var collection = il.DeclareLocal( memberType, "collection" );
				Emittion.EmitUnmarshalValue(
					emitter,
					il,
					1,
					collection,
					null // Dispatching closure shall adjust position.
				);
				il.EmitAnyLdarg( 1 );
				il.EmitAnyLdloc( collection );
				Emittion.EmitStoreValue( il, member );
				il.EmitRet();
			}
			finally
			{
				il.FlushTrace();
			}
		}
	}
}
