﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Threading.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp)]
public class VSTHRD111UseConfigureAwaitCodeFix : CodeFixProvider
{
    private static readonly ImmutableArray<string> ReusableFixableDiagnosticIds = ImmutableArray.Create(
        VSTHRD111UseConfigureAwaitAnalyzer.Id);

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => ReusableFixableDiagnosticIds;

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (Diagnostic? diagnostic in context.Diagnostics)
        {
            SemanticModel? semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            SyntaxNode syntaxRoot = await context.Document.GetSyntaxRootOrThrowAsync(context.CancellationToken).ConfigureAwait(false);
            var awaitedExpression = syntaxRoot.FindNode(diagnostic.Location.SourceSpan) as ExpressionSyntax;
            if (awaitedExpression is null)
            {
                return;
            }

            Task<Document> ApplyFix(bool captureContext)
            {
                ExpressionSyntax configuredAwaitExpression = SyntaxFactory.ParenthesizedExpression(
                    SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.ParenthesizedExpression(awaitedExpression).WithAdditionalAnnotations(Simplifier.Annotation),
                            SyntaxFactory.IdentifierName("ConfigureAwait")))
                    .AddArgumentListArguments(SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(captureContext ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression))))
                    .WithAdditionalAnnotations(Simplifier.Annotation);

                return Task.FromResult(context.Document.WithSyntaxRoot(syntaxRoot.ReplaceNode(awaitedExpression, configuredAwaitExpression)));
            }

            context.RegisterCodeFix(CodeAction.Create(Strings.VSTHRD111_CodeFix_True_Title, ct => ApplyFix(true), true.ToString()), diagnostic);
            context.RegisterCodeFix(CodeAction.Create(Strings.VSTHRD111_CodeFix_False_Title, ct => ApplyFix(false), false.ToString()), diagnostic);
        }
    }
}
