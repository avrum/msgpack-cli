﻿#region -- License Terms --
//
// MessagePack for CLI
//
// Copyright (C) 2017 FUJIWARA, Yusuke
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

#if UNITY_5 || UNITY_STANDALONE || UNITY_WEBPLAYER || UNITY_WII || UNITY_IPHONE || UNITY_ANDROID || UNITY_PS3 || UNITY_XBOX360 || UNITY_FLASH || UNITY_BKACKBERRY || UNITY_WINRT
#define UNITY
#endif

using System;
using System.IO;
#if FEATURE_TAP
using System.Threading;
using System.Threading.Tasks;
#endif // FEATURE_TAP

namespace MsgPack
{
	// This file was generated from Unpacker.Leaf.tt and Core.ttinclude T4Template.
	// Do not modify this file. Edit Unpacker.Leaf.tt and Core.ttinclude instead.


	internal sealed class FastStreamUnpacker : DefaultStreamUnpacker
	{
		public FastStreamUnpacker( Stream stream, PackerUnpackerStreamOptions streamOptions )
			: base( stream, streamOptions ) { }

		protected override Unpacker ReadSubtreeCore()
		{
			return this;
		}
	}

	internal sealed class CollectionValidatingStreamUnpacker : DefaultStreamUnpacker
	{
		// ReSharper disable once RedundantDefaultFieldInitializer
		private bool _isSubtreeReading = false;

		public CollectionValidatingStreamUnpacker( Stream stream, PackerUnpackerStreamOptions streamOptions )
			: base( stream, streamOptions ) { }

		internal override Unpacker InternalReadSubtree()
		{
			if ( !this.IsCollectionHeader )
			{
				ThrowCannotBeSubtreeModeException();
			}

			if ( this._isSubtreeReading )
			{
				ThrowInSubtreeModeException();
			}

			var subtreeReader = this.ReadSubtreeCore();
			this._isSubtreeReading = !ReferenceEquals( subtreeReader, this );
			return subtreeReader;
		}

		protected override Unpacker ReadSubtreeCore()
		{
			return new SubtreeUnpacker( this.Core, this );
		}

		protected internal override void EndReadSubtree()
		{
			this._isSubtreeReading = false;
			base.EndReadSubtree();
		}

		internal override void EnsureNotInSubtreeMode()
		{
			this.VerifyMode( UnpackerMode.Streaming );
			if ( this._isSubtreeReading )
			{
				ThrowInSubtreeModeException();
			}
		}

		internal override void BeginSkipCore()
		{
			if ( this._isSubtreeReading )
			{
				ThrowInSubtreeModeException();
			}
		}
	}

	internal sealed class FastByteArrayUnpacker : DefaultByteArrayUnpacker
	{
		public FastByteArrayUnpacker( ArraySegment<byte> source )
			: base( source ) { }

		protected override Unpacker ReadSubtreeCore()
		{
			return this;
		}
	}

	internal sealed class CollectionValidatingByteArrayUnpacker : DefaultByteArrayUnpacker
	{
		// ReSharper disable once RedundantDefaultFieldInitializer
		private bool _isSubtreeReading = false;

		public CollectionValidatingByteArrayUnpacker( ArraySegment<byte> source )
			: base( source ) { }

		internal override Unpacker InternalReadSubtree()
		{
			if ( !this.IsCollectionHeader )
			{
				ThrowCannotBeSubtreeModeException();
			}

			if ( this._isSubtreeReading )
			{
				ThrowInSubtreeModeException();
			}

			var subtreeReader = this.ReadSubtreeCore();
			this._isSubtreeReading = !ReferenceEquals( subtreeReader, this );
			return subtreeReader;
		}

		protected override Unpacker ReadSubtreeCore()
		{
			return new SubtreeUnpacker( this.Core, this );
		}

		protected internal override void EndReadSubtree()
		{
			this._isSubtreeReading = false;
			base.EndReadSubtree();
		}

		internal override void EnsureNotInSubtreeMode()
		{
			this.VerifyMode( UnpackerMode.Streaming );
			if ( this._isSubtreeReading )
			{
				ThrowInSubtreeModeException();
			}
		}

		internal override void BeginSkipCore()
		{
			if ( this._isSubtreeReading )
			{
				ThrowInSubtreeModeException();
			}
		}
	}
}