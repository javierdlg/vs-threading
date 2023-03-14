﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.VisualStudio.Threading;

/// <summary>
/// Exception thrown when a <see cref="ReentrantSemaphore"/> is in a faulted state.
/// </summary>
public class SemaphoreFaultedException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SemaphoreFaultedException"/> class.
    /// </summary>
    public SemaphoreFaultedException()
        : base(Strings.SemaphoreMisused)
    {
    }
}
