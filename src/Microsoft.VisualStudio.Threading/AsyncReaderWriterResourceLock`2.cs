﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.Threading;

/// <summary>
/// A non-blocking lock that allows concurrent access, exclusive access, or concurrent with upgradeability to exclusive access,
/// making special allowances for resources that must be prepared for concurrent or exclusive access.
/// </summary>
/// <typeparam name="TMoniker">The type of the moniker that identifies a resource.</typeparam>
/// <typeparam name="TResource">The type of resource issued for access by this lock.</typeparam>
public abstract class AsyncReaderWriterResourceLock<TMoniker, TResource> : AsyncReaderWriterLock
    where TResource : class
{
    /// <summary>
    /// A private nested class we use to isolate some of the behavior.
    /// </summary>
    private readonly Helper helper;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncReaderWriterResourceLock{TMoniker, TResource}"/> class.
    /// </summary>
    protected AsyncReaderWriterResourceLock()
    {
        this.helper = new Helper(this);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncReaderWriterResourceLock{TMoniker, TResource}"/> class.
    /// </summary>
    /// <param name="captureDiagnostics">
    /// <see langword="true" /> to spend additional resources capturing diagnostic details that can be used
    /// to analyze deadlocks or other issues.</param>
    protected AsyncReaderWriterResourceLock(bool captureDiagnostics)
        : base(captureDiagnostics)
    {
        this.helper = new Helper(this);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncReaderWriterResourceLock{TMoniker, TResource}"/> class.
    /// </summary>
    /// <param name="joinableTaskContext">
    /// A JoinableTaskContext to help resolve dead locks caused by interdependency between top read lock tasks when there is a pending write lock blocking one of them.
    /// </param>
    /// <param name="captureDiagnostics">
    /// <see langword="true" /> to spend additional resources capturing diagnostic details that can be used
    /// to analyze deadlocks or other issues.</param>
    protected AsyncReaderWriterResourceLock(JoinableTaskContext? joinableTaskContext, bool captureDiagnostics)
        : base(joinableTaskContext, captureDiagnostics)
    {
        this.helper = new Helper(this);
    }

    /// <summary>
    /// Flags that modify default lock behavior.
    /// </summary>
    [Flags]
    public new enum LockFlags
    {
        /// <summary>
        /// The default behavior applies.
        /// </summary>
        None = 0x0,

        /// <summary>
        /// Causes an upgradeable reader to remain in an upgraded-write state once upgraded,
        /// even after the nested write lock has been released.
        /// </summary>
        /// <remarks>
        /// This is useful when you have a batch of possible write operations to apply, which
        /// may or may not actually apply in the end, but if any of them change anything,
        /// all of their changes should be seen atomically (within a single write lock).
        /// This approach is preferable to simply acquiring a write lock around the batch of
        /// potential changes because it doesn't defeat concurrent readers until it knows there
        /// is a change to actually make.
        /// </remarks>
        StickyWrite = 0x1,

        /// <summary>
        /// Skips a step to make sure that the resource is initially prepared when retrieved using GetResourceAsync.
        /// </summary>
        /// <remarks>
        /// This flag is dormant for non-write locks.  But if present on an upgradeable read lock,
        /// this flag will activate for a nested write lock.
        /// </remarks>
        SkipInitialPreparation = 0x1000,
    }

    /// <summary>
    /// Obtains a read lock, asynchronously awaiting for the lock if it is not immediately available.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token whose cancellation indicates lost interest in obtaining the lock.
    /// A canceled token does not release a lock that has already been issued.  But if the lock isn't immediately available,
    /// a canceled token will cause the code that is waiting for the lock to resume with an <see cref="OperationCanceledException"/>.
    /// </param>
    /// <returns>An awaitable object whose result is the lock releaser.</returns>
    public new ResourceAwaitable ReadLockAsync(CancellationToken cancellationToken = default(CancellationToken))
    {
        return new ResourceAwaitable(base.ReadLockAsync(cancellationToken), this.helper);
    }

    /// <summary>
    /// Obtains a read lock, asynchronously awaiting for the lock if it is not immediately available.
    /// </summary>
    /// <param name="options">Modifications to normal lock behavior.</param>
    /// <param name="cancellationToken">
    /// A token whose cancellation indicates lost interest in obtaining the lock.
    /// A canceled token does not release a lock that has already been issued.  But if the lock isn't immediately available,
    /// a canceled token will cause the code that is waiting for the lock to resume with an <see cref="OperationCanceledException"/>.
    /// </param>
    /// <returns>An awaitable object whose result is the lock releaser.</returns>
    public ResourceAwaitable UpgradeableReadLockAsync(LockFlags options, CancellationToken cancellationToken = default(CancellationToken))
    {
        return new ResourceAwaitable(this.UpgradeableReadLockAsync((AsyncReaderWriterLock.LockFlags)options, cancellationToken), this.helper);
    }

    /// <summary>
    /// Obtains an upgradeable read lock, asynchronously awaiting for the lock if it is not immediately available.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token whose cancellation indicates lost interest in obtaining the lock.
    /// A canceled token does not release a lock that has already been issued.  But if the lock isn't immediately available,
    /// a canceled token will cause the code that is waiting for the lock to resume with an <see cref="OperationCanceledException"/>.
    /// </param>
    /// <returns>An awaitable object whose result is the lock releaser.</returns>
    public new ResourceAwaitable UpgradeableReadLockAsync(CancellationToken cancellationToken = default(CancellationToken))
    {
        return new ResourceAwaitable(base.UpgradeableReadLockAsync(cancellationToken), this.helper);
    }

    /// <summary>
    /// Obtains a write lock, asynchronously awaiting for the lock if it is not immediately available.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token whose cancellation indicates lost interest in obtaining the lock.
    /// A canceled token does not release a lock that has already been issued.  But if the lock isn't immediately available,
    /// a canceled token will cause the code that is waiting for the lock to resume with an <see cref="OperationCanceledException"/>.
    /// </param>
    /// <returns>An awaitable object whose result is the lock releaser.</returns>
    public new ResourceAwaitable WriteLockAsync(CancellationToken cancellationToken = default(CancellationToken))
    {
        return new ResourceAwaitable(base.WriteLockAsync(cancellationToken), this.helper);
    }

    /// <summary>
    /// Obtains a write lock, asynchronously awaiting for the lock if it is not immediately available.
    /// </summary>
    /// <param name="options">Modifications to normal lock behavior.</param>
    /// <param name="cancellationToken">
    /// A token whose cancellation indicates lost interest in obtaining the lock.
    /// A canceled token does not release a lock that has already been issued.  But if the lock isn't immediately available,
    /// a canceled token will cause the code that is waiting for the lock to resume with an <see cref="OperationCanceledException"/>.
    /// </param>
    /// <returns>An awaitable object whose result is the lock releaser.</returns>
    public ResourceAwaitable WriteLockAsync(LockFlags options, CancellationToken cancellationToken = default(CancellationToken))
    {
        return new ResourceAwaitable(this.WriteLockAsync((AsyncReaderWriterLock.LockFlags)options, cancellationToken), this.helper);
    }

    /// <summary>
    /// Retrieves the resource with the specified moniker.
    /// </summary>
    /// <param name="resourceMoniker">The identifier for the desired resource.</param>
    /// <param name="cancellationToken">A token whose cancellation indicates lost interest in obtaining the resource.</param>
    /// <returns>A task whose result is the desired resource.</returns>
    protected abstract Task<TResource> GetResourceAsync(TMoniker resourceMoniker, CancellationToken cancellationToken);

    /// <summary>
    /// Marks a resource as having been retrieved under a lock.
    /// </summary>
    protected void SetResourceAsAccessed(TResource resource)
    {
        this.helper.SetResourceAsAccessed(resource);
    }

    /// <summary>
    /// Marks any loaded resources as having been retrieved under a lock if they
    /// satisfy some predicate.
    /// </summary>
    /// <param name="resourceCheck">A function that returns <see langword="true" /> if the provided resource should be considered retrieved.</param>
    /// <param name="state">The state object to pass as a second parameter to <paramref name="resourceCheck"/>.</param>
    /// <returns><see langword="true" /> if the delegate returned <see langword="true" /> on any of the invocations.</returns>
    protected bool SetResourceAsAccessed(Func<TResource, object?, bool> resourceCheck, object? state)
    {
        return this.helper.SetResourceAsAccessed(resourceCheck, state);
    }

    /// <summary>
    /// Sets all the resources to be considered in an unknown state.
    /// </summary>
    protected void SetAllResourcesToUnknownState()
    {
        Verify.Operation(this.IsWriteLockHeld, Strings.InvalidLock);
        this.helper.SetAllResourcesToUnknownState();
    }

    /// <summary>
    /// Returns the aggregate of the lock flags for all nested locks.
    /// </summary>
    protected new LockFlags GetAggregateLockFlags()
    {
        return (LockFlags)base.GetAggregateLockFlags();
    }

    /// <summary>
    /// Gets a task scheduler to prepare a resource for concurrent access.
    /// </summary>
    /// <param name="resource">The resource to prepare.</param>
    /// <returns>A <see cref="TaskScheduler"/>.</returns>
    protected virtual TaskScheduler GetTaskSchedulerToPrepareResourcesForConcurrentAccess(TResource resource)
    {
        return TaskScheduler.Default;
    }

    /// <summary>
    /// Prepares a resource for concurrent access.
    /// </summary>
    /// <param name="resource">The resource to prepare.</param>
    /// <param name="cancellationToken">The token whose cancellation signals lost interest in the resource.</param>
    /// <returns>A task whose completion signals the resource has been prepared.</returns>
    /// <remarks>
    /// This is invoked on a resource when it is initially requested for concurrent access,
    /// for both transitions from no access and exclusive access.
    /// </remarks>
    protected abstract Task PrepareResourceForConcurrentAccessAsync(TResource resource, CancellationToken cancellationToken);

    /// <summary>
    /// Prepares a resource for access by one thread.
    /// </summary>
    /// <param name="resource">The resource to prepare.</param>
    /// <param name="lockFlags">The aggregate of all flags from the active and nesting locks.</param>
    /// <param name="cancellationToken">The token whose cancellation signals lost interest in the resource.</param>
    /// <returns>A task whose completion signals the resource has been prepared.</returns>
    /// <remarks>
    /// This is invoked on a resource when it is initially access for exclusive access,
    /// but only when transitioning from no access -- it is not invoked when transitioning
    /// from concurrent access to exclusive access.
    /// </remarks>
    protected abstract Task PrepareResourceForExclusiveAccessAsync(TResource resource, LockFlags lockFlags, CancellationToken cancellationToken);

    /// <summary>
    /// Invoked after an exclusive lock is released but before anyone has a chance to enter the lock.
    /// </summary>
    /// <remarks>
    /// This method is called while holding a private lock in order to block future lock consumers till this method is finished.
    /// </remarks>
    protected override async Task OnExclusiveLockReleasedAsync()
    {
        await base.OnExclusiveLockReleasedAsync().ConfigureAwait(false);
        await this.helper.OnExclusiveLockReleasedAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Invoked when a top-level upgradeable read lock is released, leaving no remaining (write) lock.
    /// </summary>
    protected override void OnUpgradeableReadLockReleased()
    {
        base.OnUpgradeableReadLockReleased();
        this.helper.OnUpgradeableReadLockReleased();
    }

    /// <summary>
    /// An awaitable that is returned from asynchronous lock requests.
    /// </summary>
    public readonly struct ResourceAwaitable
    {
        /// <summary>
        /// The underlying lock awaitable.
        /// </summary>
        private readonly AsyncReaderWriterLock.Awaitable awaitable;

        /// <summary>
        /// The helper class.
        /// </summary>
        private readonly Helper helper;

        /// <summary>
        /// Initializes a new instance of the <see cref="ResourceAwaitable"/> struct.
        /// </summary>
        /// <param name="awaitable">The underlying lock awaitable.</param>
        /// <param name="helper">The helper class.</param>
        internal ResourceAwaitable(AsyncReaderWriterLock.Awaitable awaitable, Helper helper)
        {
            this.awaitable = awaitable;
            this.helper = helper;
        }

        /// <summary>
        /// Gets the awaiter value.
        /// </summary>
        public ResourceAwaiter GetAwaiter()
        {
            return new ResourceAwaiter(this.awaitable.GetAwaiter(), this.helper);
        }
    }

    /// <summary>
    /// Manages asynchronous access to a lock.
    /// </summary>
    [DebuggerDisplay("{awaiter.kind}")]
    public readonly struct ResourceAwaiter : ICriticalNotifyCompletion
    {
        /// <summary>
        /// The underlying lock awaiter.
        /// </summary>
        private readonly AsyncReaderWriterLock.Awaiter awaiter;

        /// <summary>
        /// The helper class.
        /// </summary>
        private readonly Helper helper;

        /// <summary>
        /// Initializes a new instance of the <see cref="ResourceAwaiter"/> struct.
        /// </summary>
        /// <param name="awaiter">The underlying lock awaiter.</param>
        /// <param name="helper">The helper class.</param>
        internal ResourceAwaiter(AsyncReaderWriterLock.Awaiter awaiter, Helper helper)
        {
            Requires.NotNull(awaiter, nameof(awaiter));
            Requires.NotNull(helper, nameof(helper));

            this.awaiter = awaiter;
            this.helper = helper;
        }

        /// <summary>
        /// Gets a value indicating whether the lock has been issued.
        /// </summary>
        public bool IsCompleted
        {
            get
            {
                if (this.awaiter is null)
                {
                    throw new InvalidOperationException();
                }

                return this.awaiter.IsCompleted;
            }
        }

        /// <summary>
        /// Sets the delegate to execute when the lock is available.
        /// </summary>
        /// <param name="continuation">The delegate.</param>
        public void OnCompleted(Action continuation)
        {
            if (this.awaiter is null)
            {
                throw new InvalidOperationException();
            }

            this.awaiter.OnCompleted(continuation);
        }

        /// <summary>
        /// Sets the delegate to execute when the lock is available.
        /// </summary>
        /// <param name="continuation">The delegate.</param>
        public void UnsafeOnCompleted(Action continuation)
        {
            if (this.awaiter is null)
            {
                throw new InvalidOperationException();
            }

            this.awaiter.UnsafeOnCompleted(continuation);
        }

        /// <summary>
        /// Applies the issued lock to the caller and returns the value used to release the lock.
        /// </summary>
        /// <returns>The value to dispose of to release the lock.</returns>
        public ResourceReleaser GetResult()
        {
            if (this.awaiter is null)
            {
                throw new InvalidOperationException();
            }

            return new ResourceReleaser(this.awaiter.GetResult(), this.helper);
        }
    }

    /// <summary>
    /// A value whose disposal releases a held lock.
    /// </summary>
    [DebuggerDisplay("{releaser.awaiter.kind}")]
    public readonly struct ResourceReleaser : IDisposable, System.IAsyncDisposable
    {
        /// <summary>
        /// The underlying lock releaser.
        /// </summary>
        private readonly AsyncReaderWriterLock.Releaser releaser;

        /// <summary>
        /// The helper class.
        /// </summary>
        private readonly Helper helper;

        /// <summary>
        /// Initializes a new instance of the <see cref="ResourceReleaser"/> struct.
        /// </summary>
        /// <param name="releaser">The underlying lock releaser.</param>
        /// <param name="helper">The helper class.</param>
        internal ResourceReleaser(AsyncReaderWriterLock.Releaser releaser, Helper helper)
        {
            this.releaser = releaser;
            this.helper = helper;
        }

        /// <summary>
        /// Gets the underlying lock releaser.
        /// </summary>
        internal AsyncReaderWriterLock.Releaser LockReleaser
        {
            get { return this.releaser; }
        }

        /// <summary>
        /// Gets the lock protected resource.
        /// </summary>
        /// <param name="resourceMoniker">The identifier for the protected resource.</param>
        /// <param name="cancellationToken">A token whose cancellation signals lost interest in the protected resource.</param>
        /// <returns>A task whose result is the resource.</returns>
        public Task<TResource> GetResourceAsync(TMoniker resourceMoniker, CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.helper.GetResourceAsync(resourceMoniker, cancellationToken);
        }

        /// <summary>
        /// Releases the lock.
        /// </summary>
        public void Dispose()
        {
            this.LockReleaser.Dispose();
        }

        /// <summary>
        /// Releases the lock.
        /// </summary>
        public ValueTask DisposeAsync() => this.LockReleaser.DisposeAsync();

        /// <summary>
        /// Asynchronously releases the lock.  Dispose should still be called after this.
        /// </summary>
        /// <remarks>
        /// Rather than calling this method explicitly, use the C# 8 "await using" syntax instead.
        /// </remarks>
        public Task ReleaseAsync()
        {
            return this.LockReleaser.ReleaseAsync();
        }
    }

    /// <summary>
    /// A helper class to isolate some specific functionality in this outer class.
    /// </summary>
    internal class Helper
    {
        /// <summary>
        /// The owning lock instance.
        /// </summary>
        private readonly AsyncReaderWriterResourceLock<TMoniker, TResource> service;

        /// <summary>
        /// A reusable delegate that invokes the <see cref="AsyncReaderWriterResourceLock{TMoniker, TResource}.PrepareResourceForConcurrentAccessAsync"/> method.
        /// </summary>
        private readonly Func<object, Task> prepareResourceConcurrentDelegate;

        /// <summary>
        /// A reusable delegate that invokes the <see cref="AsyncReaderWriterResourceLock{TMoniker, TResource}.PrepareResourceForExclusiveAccessAsync"/> method.
        /// </summary>
        private readonly Func<object, Task> prepareResourceExclusiveDelegate;

        /// <summary>
        /// A reusable delegate that invokes the <see cref="AsyncReaderWriterResourceLock{TMoniker, TResource}.PrepareResourceForConcurrentAccessAsync"/> method.
        /// </summary>
        private readonly Func<Task, object, Task> prepareResourceConcurrentContinuationDelegate;

        /// <summary>
        /// A reusable delegate that invokes the <see cref="AsyncReaderWriterResourceLock{TMoniker, TResource}.PrepareResourceForExclusiveAccessAsync"/> method.
        /// </summary>
        private readonly Func<Task, object, Task> prepareResourceExclusiveContinuationDelegate;

        /// <summary>
        /// A reusable delegate that invokes the <see cref="AsyncReaderWriterResourceLock{TMoniker, TResource}.PrepareResourceForConcurrentAccessAsync"/> method.
        /// </summary>
        private readonly Func<Task, object, Task> prepareResourceConcurrentContinuationOnPossibleCancelledTaskDelegate;

        /// <summary>
        /// A reusable delegate that invokes the <see cref="AsyncReaderWriterResourceLock{TMoniker, TResource}.PrepareResourceForExclusiveAccessAsync"/> method.
        /// </summary>
        private readonly Func<Task, object, Task> prepareResourceExclusiveContinuationOnPossibleCancelledTaskDelegateDelegate;

        /// <summary>
        /// A collection of all the resources requested within the outermost upgradeable read lock.
        /// </summary>
        private readonly HashSet<TResource> resourcesAcquiredWithinUpgradeableRead = new HashSet<TResource>();

        /// <summary>
        /// A map of resources to the status of tasks that most recently began evaluating them.
        /// </summary>
        private readonly WeakKeyDictionary<TResource, ResourcePreparationTaskState> resourcePreparationStates = new WeakKeyDictionary<TResource, ResourcePreparationTaskState>(capacity: 2);

        /// <summary>
        /// Initializes a new instance of the <see cref="Helper"/> class.
        /// </summary>
        /// <param name="service">The owning lock instance.</param>
        internal Helper(AsyncReaderWriterResourceLock<TMoniker, TResource> service)
        {
            Requires.NotNull(service, nameof(service));

            this.service = service;
            this.prepareResourceConcurrentDelegate = state =>
            {
                var tuple = (Tuple<TResource, CancellationToken>)state;
                return this.service.PrepareResourceForConcurrentAccessAsync(tuple.Item1, tuple.Item2);
            };

            this.prepareResourceExclusiveDelegate = state =>
            {
                var tuple = (Tuple<TResource, LockFlags, CancellationToken>)state;
                return this.service.PrepareResourceForExclusiveAccessAsync(tuple.Item1, tuple.Item2, tuple.Item3);
            };

            this.prepareResourceConcurrentContinuationDelegate = (prev, state) =>
            {
                var tuple = (Tuple<TResource, CancellationToken>)state;
                return this.service.PrepareResourceForConcurrentAccessAsync(tuple.Item1, tuple.Item2);
            };

            this.prepareResourceExclusiveContinuationDelegate = (prev, state) =>
            {
                var tuple = (Tuple<TResource, LockFlags, CancellationToken>)state;
                return this.service.PrepareResourceForExclusiveAccessAsync(tuple.Item1, tuple.Item2, tuple.Item3);
            };

            // this delegate is to handle the case that we prepare resource when the previous task might be cancelled.
            // Because the previous task might not be cancelled, but actually finished. In that case, we will consider the work has done, and there is no need to prepare it again.
            this.prepareResourceConcurrentContinuationOnPossibleCancelledTaskDelegate = (prev, state) =>
            {
                if (!prev.IsFaulted && !prev.IsCanceled)
                {
                    return prev;
                }

                var tuple = (Tuple<TResource, CancellationToken>)state;
                return this.service.PrepareResourceForConcurrentAccessAsync(tuple.Item1, tuple.Item2);
            };

            this.prepareResourceExclusiveContinuationOnPossibleCancelledTaskDelegateDelegate = (prev, state) =>
            {
                if (!prev.IsFaulted && !prev.IsCanceled)
                {
                    return prev;
                }

                var tuple = (Tuple<TResource, LockFlags, CancellationToken>)state;
                return this.service.PrepareResourceForExclusiveAccessAsync(tuple.Item1, tuple.Item2, tuple.Item3);
            };
        }

        /// <summary>
        /// Describes the states a resource can be in.
        /// </summary>
        private enum ResourceState
        {
            /// <summary>
            /// The resource is neither prepared for concurrent nor exclusive access.
            /// </summary>
            Unknown,

            /// <summary>
            /// The resource is prepared for concurrent access.
            /// </summary>
            Concurrent,

            /// <summary>
            /// The resource is prepared for exclusive access.
            /// </summary>
            Exclusive,
        }

        /// <summary>
        /// Marks a resource as having been retrieved under a lock.
        /// </summary>
        internal void SetResourceAsAccessed(TResource resource)
        {
            Requires.NotNull(resource, nameof(resource));

            // Capture the ambient lock and use it for the two lock checks rather than
            // call AsyncReaderWriterLock.IsWriteLockHeld and IsUpgradeableReadLockHeld
            // to reduce the number of slow AsyncLocal<T>.get_Value calls we make.
            // Also do it before we acquire the lock, since a lock isn't necessary.
            // (verified to be a perf bottleneck in ETL traces).
            LockHandle ambientLock = this.service.AmbientLock;
            lock (this.service.SyncObject)
            {
                if (!ambientLock.HasWriteLock && ambientLock.HasUpgradeableReadLock)
                {
                    this.resourcesAcquiredWithinUpgradeableRead.Add(resource);
                }
            }
        }

        /// <summary>
        /// Marks any loaded resources as having been retrieved under a lock if they
        /// satisfy some predicate.
        /// </summary>
        /// <param name="resourceCheck">A function that returns <see langword="true" /> if the provided resource should be considered retrieved.</param>
        /// <param name="state">The state object to pass as a second parameter to <paramref name="resourceCheck"/>.</param>
        /// <returns><see langword="true" /> if the delegate returned <see langword="true" /> on any of the invocations.</returns>
        internal bool SetResourceAsAccessed(Func<TResource, object?, bool> resourceCheck, object? state)
        {
            Requires.NotNull(resourceCheck, nameof(resourceCheck));

            // Capture the ambient lock and use it for the two lock checks rather than
            // call AsyncReaderWriterLock.IsWriteLockHeld and IsUpgradeableReadLockHeld
            // to reduce the number of slow AsyncLocal<T>.get_Value calls we make.
            // Also do it before we acquire the lock, since a lock isn't necessary.
            // (verified to be a perf bottleneck in ETL traces).
            LockHandle ambientLock = this.service.AmbientLock;
            bool match = false;
            lock (this.service.SyncObject)
            {
                if (ambientLock.HasWriteLock || ambientLock.HasUpgradeableReadLock)
                {
                    foreach (KeyValuePair<TResource, AsyncReaderWriterResourceLock<TMoniker, TResource>.Helper.ResourcePreparationTaskState> resource in this.resourcePreparationStates)
                    {
                        if (resourceCheck(resource.Key, state))
                        {
                            match = true;
                            this.SetResourceAsAccessed(resource.Key);
                        }
                    }
                }
            }

            return match;
        }

        /// <summary>
        /// Ensures that all resources are marked as unprepared so at next request they are prepared again.
        /// </summary>
        internal Task OnExclusiveLockReleasedAsync()
        {
            lock (this.service.SyncObject)
            {
                // Reset ALL resources to an unknown state. Not just the ones explicitly requested
                // because backdoors can and legitimately do (as in CPS) exist for tampering
                // with a resource without going through our access methods.
                this.SetAllResourcesToUnknownState();

                if (this.service.IsUpgradeableReadLockHeld && this.resourcesAcquiredWithinUpgradeableRead.Count > 0)
                {
                    // We must also synchronously prepare all resources that were acquired within the upgradeable read lock
                    // because as soon as this method returns these resources may be access concurrently again.
                    var preparationTasks = new Task[this.resourcesAcquiredWithinUpgradeableRead.Count];
                    int taskIndex = 0;
                    foreach (TResource? resource in this.resourcesAcquiredWithinUpgradeableRead)
                    {
                        preparationTasks[taskIndex++] = this.PrepareResourceAsync(resource, CancellationToken.None, forcePrepareConcurrent: true);
                    }

                    if (preparationTasks.Length == 1)
                    {
                        return preparationTasks[0];
                    }
                    else if (preparationTasks.Length > 1)
                    {
                        return Task.WhenAll(preparationTasks);
                    }
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Invoked when a top-level upgradeable read lock is released, leaving no remaining (write) lock.
        /// </summary>
        internal void OnUpgradeableReadLockReleased()
        {
            this.resourcesAcquiredWithinUpgradeableRead.Clear();
        }

        /// <summary>
        /// Retrieves the resource with the specified moniker.
        /// </summary>
        /// <param name="resourceMoniker">The identifier for the desired resource.</param>
        /// <param name="cancellationToken">The token whose cancellation signals lost interest in this resource.</param>
        /// <returns>A task whose result is the desired resource.</returns>
        internal async Task<TResource> GetResourceAsync(TMoniker resourceMoniker, CancellationToken cancellationToken)
        {
            using (AsyncReaderWriterResourceLock<TMoniker, TResource>.ResourceReleaser resourceLock = this.AcquirePreexistingLockOrThrow())
            {
                TResource? resource = await this.service.GetResourceAsync(resourceMoniker, cancellationToken).ConfigureAwaitRunInline();
                Task preparationTask;

                lock (this.service.SyncObject)
                {
                    this.SetResourceAsAccessed(resource);

                    preparationTask = this.PrepareResourceAsync(resource, cancellationToken);
                }

                await preparationTask.ConfigureAwaitRunInline();
                return resource;
            }
        }

        /// <summary>
        /// Sets all the resources to be considered in an unknown state. Any subsequent access (exclusive or concurrent) will prepare the resource.
        /// </summary>
        internal void SetAllResourcesToUnknownState()
        {
            this.SetUnknownResourceState(this.resourcePreparationStates.Select(rp => rp.Key).ToList());
        }

        /// <summary>
        /// Sets the specified resource to be considered in an unknown state. Any subsequent access (exclusive or concurrent) will prepare the resource.
        /// </summary>
        private void SetUnknownResourceState(TResource resource)
        {
            Requires.NotNull(resource, nameof(resource));

            lock (this.service.SyncObject)
            {
                this.resourcePreparationStates.TryGetValue(resource, out ResourcePreparationTaskState? previousState);
                this.resourcePreparationStates[resource] = ResourcePreparationTaskState.Create(
                    _ => previousState?.InnerTask ?? Task.CompletedTask,
                    ResourceState.Unknown,
                    TaskScheduler.Default,
                    CancellationToken.None).PreparationState;
            }
        }

        /// <summary>
        /// Sets the specified resources to be considered in an unknown state. Any subsequent access (exclusive or concurrent) will prepare the resource.
        /// </summary>
        private void SetUnknownResourceState(IEnumerable<TResource> resources)
        {
            Requires.NotNull(resources, nameof(resources));
            foreach (TResource? resource in resources)
            {
                this.SetUnknownResourceState(resource);
            }
        }

        /// <summary>
        /// Prepares the specified resource for access by a lock holder.
        /// </summary>
        /// <param name="resource">The resource to prepare.</param>
        /// <param name="cancellationToken">The token whose cancellation signals lost interest in this resource.</param>
        /// <param name="forcePrepareConcurrent">Force preparation of the resource for concurrent access, even if an exclusive lock is currently held.</param>
        /// <returns>A task that is completed when preparation has completed.</returns>
        private Task PrepareResourceAsync(TResource resource, CancellationToken cancellationToken, bool forcePrepareConcurrent = false)
        {
            Requires.NotNull(resource, nameof(resource));
            Assumes.True(Monitor.IsEntered(this.service.SyncObject));

            // We deliberately ignore the cancellation token in the tasks we create and save because the tasks can be shared
            // across requests and we can't have task continuation chains where tasks within the chain get canceled
            // as that can cause premature starting of the next task in the chain.
            bool forConcurrentUse = forcePrepareConcurrent || !this.service.IsWriteLockHeld;
            AsyncReaderWriterResourceLock<TMoniker, TResource>.Helper.ResourceState finalState = forConcurrentUse ? ResourceState.Concurrent : ResourceState.Exclusive;

            Task? preparationTask = null;
            TaskScheduler taskScheduler = this.service.GetTaskSchedulerToPrepareResourcesForConcurrentAccess(resource);

            if (!this.resourcePreparationStates.TryGetValue(resource, out ResourcePreparationTaskState? preparationState))
            {
                Func<object, Task>? preparationDelegate = forConcurrentUse
                    ? this.prepareResourceConcurrentDelegate
                    : this.prepareResourceExclusiveDelegate;

                // We kick this off on a new task because we're currently holding a private lock
                // and don't want to execute arbitrary code.
                // Let's also hide the ARWL from the delegate if this is a shared lock request.
                using (forConcurrentUse ? this.service.HideLocks() : default)
                {
                    // We can't currently use the caller's cancellation token for this task because
                    // this task may be shared with others or call this method later, and we wouldn't
                    // want their requests to be cancelled as a result of this first caller cancelling.
                    (preparationState, preparationTask) = ResourcePreparationTaskState.Create(
                        combinedCancellationToken => Task.Factory.StartNew(
                            NullableHelpers.AsNullableArgFunc(preparationDelegate),
                            forConcurrentUse ? Tuple.Create(resource, combinedCancellationToken) : Tuple.Create(resource, this.service.GetAggregateLockFlags(), combinedCancellationToken),
                            combinedCancellationToken,
                            TaskCreationOptions.None,
                            taskScheduler).Unwrap(),
                        finalState,
                        taskScheduler,
                        cancellationToken);
                }
            }
            else
            {
                Func<Task, object, Task>? preparationDelegate = null;
                if (preparationState.State != finalState || preparationState.InnerTask.IsFaulted)
                {
                    preparationDelegate = forConcurrentUse
                        ? this.prepareResourceConcurrentContinuationDelegate
                        : this.prepareResourceExclusiveContinuationDelegate;
                }
                else if (!preparationState.TryJoinPreparationTask(out preparationTask, taskScheduler, cancellationToken))
                {
                    preparationDelegate = forConcurrentUse
                        ? this.prepareResourceConcurrentContinuationOnPossibleCancelledTaskDelegate
                        : this.prepareResourceExclusiveContinuationOnPossibleCancelledTaskDelegateDelegate;
                }

                if (preparationTask is null)
                {
                    Assumes.NotNull(preparationDelegate);

                    // We kick this off on a new task because we're currently holding a private lock
                    // and don't want to execute arbitrary code.
                    // Let's also hide the ARWL from the delegate if this is a shared lock request.
                    using (forConcurrentUse ? this.service.HideLocks() : default)
                    {
                        (preparationState, preparationTask) = ResourcePreparationTaskState.Create(
                            combinedCancellationToken => preparationState.InnerTask.ContinueWith(
                                preparationDelegate!,
                                forConcurrentUse ? Tuple.Create(resource, combinedCancellationToken) : Tuple.Create(resource, this.service.GetAggregateLockFlags(), combinedCancellationToken),
                                CancellationToken.None,
                                TaskContinuationOptions.RunContinuationsAsynchronously,
                                taskScheduler).Unwrap(),
                            finalState,
                            taskScheduler,
                            cancellationToken);
                    }
                }
            }

            Assumes.NotNull(preparationState);
            this.resourcePreparationStates[resource] = preparationState;

            return preparationTask;
        }

        /// <summary>
        /// Reserves a read lock from a previously held lock.
        /// </summary>
        /// <returns>The releaser for the read lock.</returns>
        /// <exception cref="InvalidOperationException">Thrown if no lock is held by the caller.</exception>
        private ResourceReleaser AcquirePreexistingLockOrThrow()
        {
            if (!this.service.IsAnyLockHeld)
            {
                Verify.FailOperation(Strings.InvalidWithoutLock);
            }

            AsyncReaderWriterResourceLock<TMoniker, TResource>.ResourceAwaiter awaiter = this.service.ReadLockAsync(CancellationToken.None).GetAwaiter();
            Assumes.True(awaiter.IsCompleted);
            return awaiter.GetResult();
        }

        /// <summary>
        /// Tracks a task that prepares a resource for either concurrent or exclusive use.
        /// </summary>
        private class ResourcePreparationTaskState : CancellableJoinComputation
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ResourcePreparationTaskState"/> class.
            /// </summary>
            internal ResourcePreparationTaskState(Func<CancellationToken, Task> taskCreation, ResourceState finalState, bool canBeCancelled)
                : base(taskCreation, canBeCancelled)
            {
                this.State = finalState;
            }

            /// <summary>
            /// Gets the state the resource will be in when inner task has completed.
            /// </summary>
            internal ResourceState State { get; }

            /// <summary>
            /// Creates a task to prepare the source and returns it with <see cref="ResourcePreparationTaskState"/>.
            /// </summary>
            /// <param name="taskCreation">A callback method to create the preparation task.</param>
            /// <param name="finalState">The final resource state when the preparation is done.</param>
            /// <param name="taskScheduler">A task scheduler for continuation.</param>
            /// <param name="cancellationToken">A cancellation token to abort the preparation task.</param>
            /// <returns>The preparation task and its status to be used to join more waiting tasks later.</returns>
            internal static (ResourcePreparationTaskState PreparationState, Task InitialTask) Create(Func<CancellationToken, Task> taskCreation, ResourceState finalState, TaskScheduler taskScheduler, CancellationToken cancellationToken)
            {
                var preparationState = new ResourcePreparationTaskState(taskCreation, finalState, cancellationToken.CanBeCanceled);
                Assumes.True(preparationState.TryJoinComputation(isInitialTask: true, out Task? initialTask, taskScheduler, cancellationToken));

                return (preparationState, initialTask);
            }

            /// <summary>
            /// Try to join an existing preparation task.
            /// </summary>
            /// <param name="task">The new waiting task to be completed when the resource preparation is done.</param>
            /// <param name="taskScheduler">A task scheduler for continuation.</param>
            /// <param name="cancellationToken">A cancellation token to abandon the new waiting task.</param>
            /// <returns>True if it joins successfully, it return false, if the current task has been cancelled.</returns>
            internal bool TryJoinPreparationTask([NotNullWhen(true)] out Task? task, TaskScheduler taskScheduler, CancellationToken cancellationToken)
            {
                return this.TryJoinComputation(isInitialTask: false, out task, taskScheduler, cancellationToken);
            }
        }
    }
}
