﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace Microsoft.VisualStudio.Threading;

/// <summary>
/// Defines an asynchronous method to release allocated resources.
/// </summary>
/// <remarks>
/// Consider implementing <see cref="System.IAsyncDisposable"/> instead.
/// </remarks>
public interface IAsyncDisposable
{
    /// <summary>
    /// Performs application-defined tasks associated with freeing,
    /// releasing, or resetting unmanaged resources asynchronously.
    /// </summary>
    Task DisposeAsync();
}
