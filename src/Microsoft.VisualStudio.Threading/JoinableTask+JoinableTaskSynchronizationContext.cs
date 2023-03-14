﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.VisualStudio.Threading
{
    public partial class JoinableTask
    {
        /// <summary>
        /// A synchronization context that forwards posted messages to the ambient job.
        /// </summary>
        internal class JoinableTaskSynchronizationContext : SynchronizationContext
        {
            /// <summary>
            /// The owning job factory.
            /// </summary>
            private readonly JoinableTaskFactory jobFactory;

            /// <summary>
            /// A flag indicating whether messages posted to this instance should execute
            /// on the main thread.
            /// </summary>
            private readonly bool mainThreadAffinitized;

            /// <summary>
            /// The owning job. May be null from the beginning, or cleared after task completion.
            /// </summary>
            private JoinableTask? job;

            /// <summary>
            /// Initializes a new instance of the <see cref="JoinableTaskSynchronizationContext"/> class
            /// that is affinitized to the main thread.
            /// </summary>
            /// <param name="owner">The <see cref="JoinableTaskFactory"/> that created this instance.</param>
            internal JoinableTaskSynchronizationContext(JoinableTaskFactory owner)
            {
                Requires.NotNull(owner, nameof(owner));

                this.jobFactory = owner;
                this.mainThreadAffinitized = true;
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="JoinableTaskSynchronizationContext"/> class.
            /// </summary>
            /// <param name="joinableTask">The <see cref="JoinableTask"/> that owns this instance.</param>
            /// <param name="mainThreadAffinitized">A value indicating whether messages posted to this instance should execute on the main thread.</param>
            internal JoinableTaskSynchronizationContext(JoinableTask joinableTask, bool mainThreadAffinitized)
                : this(Requires.NotNull(joinableTask, "joinableTask").Factory)
            {
                this.job = joinableTask;
                this.mainThreadAffinitized = mainThreadAffinitized;
            }

            /// <summary>
            /// Gets a value indicating whether messages posted to this instance should execute
            /// on the main thread.
            /// </summary>
            internal bool MainThreadAffinitized
            {
                get { return this.mainThreadAffinitized; }
            }

            /// <summary>
            /// Forwards the specified message to the job this instance belongs to if applicable; otherwise to the factory.
            /// </summary>
            public override void Post(SendOrPostCallback d, object? state)
            {
                JoinableTask? job = this.job; // capture as local in case field becomes null later.
                if (job is object)
                {
                    job.Post(d, state, this.mainThreadAffinitized);
                }
                else
                {
                    this.jobFactory.Post(d, state, this.mainThreadAffinitized);
                }
            }

            /// <summary>
            /// Forwards a message to the ambient job and blocks on its execution.
            /// </summary>
            public override void Send(SendOrPostCallback d, object? state)
            {
                Requires.NotNull(d, nameof(d));

                // Some folks unfortunately capture the SynchronizationContext from the UI thread
                // while this one is active.  So forward it to the underlying sync context to not break those folks.
                // Ideally this method would throw because synchronously crossing threads is a bad idea.
                if (this.mainThreadAffinitized)
                {
                    if (this.jobFactory.Context.IsOnMainThread)
                    {
                        d(state);
                    }
                    else
                    {
                        if (this.jobFactory.Context.UnderlyingSynchronizationContext is SynchronizationContext syncContext)
                        {
                            syncContext.Send(d, state);
                        }
                        else
                        {
                            throw new NotSupportedException(Strings.SyncContextNotSet);
                        }
                    }
                }
                else
                {
                    bool isThreadPoolThread = Thread.CurrentThread.IsThreadPoolThread;
                    if (isThreadPoolThread)
                    {
                        d(state);
                    }
                    else
                    {
                        Task.Factory.StartNew(
                            s =>
                            {
                                var tuple = (Tuple<SendOrPostCallback, object>)s!;
                                tuple.Item1(tuple.Item2);
                            },
                            Tuple.Create<SendOrPostCallback, object?>(d, state),
                            CancellationToken.None,
                            TaskCreationOptions.None,
                            TaskScheduler.Default).Wait();
                    }
                }
            }

            /// <summary>
            /// Called by the joinable task when it has completed.
            /// </summary>
            internal void OnCompleted()
            {
                // Clear out our reference to the job.
                // This SynchronizationContext may have been "captured" as part of an ExecutionContext
                // and stored away someplace indefinitely, and the task we're holding may be a
                // JoinableTask<T> where T is a very expensive object or (worse) be part of the last
                // chain holding a huge object graph in memory.
                // In any case, this sync context does not need a reference to completed jobs since
                // once a job has completed, posting to it is equivalent to posting to the job's factory.
                // And clearing out this field will cause our own Post method to post to the factory
                // so behavior is equivalent.
                this.job = null;
            }
        }
    }
}
