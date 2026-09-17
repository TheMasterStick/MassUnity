using System;
using MassRPG.Data.Publishing;
using NUnit.Framework;

namespace MassRPG.Tests
{
    public sealed class DataReviewCandidateTests
    {
        [Test]
        public void Candidate_IsReproducibleAndRequiresAllFilesReadyBeforePublishReview()
        {
            var candidate = new DataReviewCandidate(
                "candidate-1",
                "Night shift balance pass",
                1_800_000_000_000,
                "data-editor/work",
                new string('a', 40),
                new[]
                {
                    new ReviewCandidateFile("ContentData/Drafts/items/sword.json", new string('b', 64), 123, ReviewCandidateDocumentState.ReadyForReview),
                    new ReviewCandidateFile("ContentData/Drafts/recipes/sword.json", new string('c', 64), 234, ReviewCandidateDocumentState.Draft)
                });

            Assert.AreEqual(2, candidate.FileCount);
            Assert.IsFalse(candidate.AllDraftsReady);
            Assert.IsFalse(candidate.IsEligibleForPublishReview);
            Assert.Throws<InvalidOperationException>(() => candidate.BeginPublishedManifest(new PublishedDataVersion(1), 1, "publish"));
        }

        [Test]
        public void Candidate_AllReady_CanBeginPublishedManifestWithoutPublishingAnything()
        {
            var candidate = new DataReviewCandidate(
                "candidate-2",
                "Ready set",
                1_800_000_000_000,
                "data-editor/work",
                new string('a', 40),
                new[]
                {
                    new ReviewCandidateFile("ContentData/Drafts/items/sword.json", new string('b', 64), 123, ReviewCandidateDocumentState.ReadyForReview)
                });

            var manifest = candidate.BeginPublishedManifest(new PublishedDataVersion(7), 1_800_000_000_100, "reviewed content");

            Assert.IsTrue(candidate.AllDraftsReady);
            Assert.IsTrue(candidate.IsEligibleForPublishReview);
            Assert.IsFalse(candidate.ContainsDesignOnlyDefinitions);
            Assert.AreEqual(7, manifest.Version.Value);
            Assert.AreEqual("reviewed content", manifest.Label);
        }

        [Test]
        public void Candidate_GenericDefinitionsRemainReviewableButCannotStartLivePublication()
        {
            var candidate = new DataReviewCandidate(
                "candidate-design-only",
                "Work-time quest and shop concepts",
                1_800_000_000_000,
                "data-editor/work",
                new string('a', 40),
                new[]
                {
                    new ReviewCandidateFile("ContentData/Drafts/definitions/blacksmith_shop.json", new string('b', 64), 321, ReviewCandidateDocumentState.ReadyForReview)
                },
                designOnlyDefinitionCount: 1);

            Assert.IsTrue(candidate.AllDraftsReady);
            Assert.IsTrue(candidate.ContainsDesignOnlyDefinitions);
            Assert.AreEqual(1, candidate.DesignOnlyDefinitionCount);
            Assert.IsFalse(candidate.IsEligibleForPublishReview);
            Assert.Throws<InvalidOperationException>(() =>
                candidate.BeginPublishedManifest(new PublishedDataVersion(8), 1_800_000_000_100, "must not publish"));
        }

        [Test]
        public void Candidate_RejectsNegativeDesignOnlyCount()
        {
            var file = new ReviewCandidateFile("x.json", new string('d', 64), 1, ReviewCandidateDocumentState.ReadyForReview);
            Assert.Throws<ArgumentOutOfRangeException>(() => new DataReviewCandidate(
                "candidate-negative",
                "invalid design count",
                0,
                "branch",
                new string('e', 40),
                new[] { file },
                designOnlyDefinitionCount: -1));
        }

        [Test]
        public void Candidate_RejectsInvalidHashesAndDuplicateFilePaths()
        {
            Assert.Throws<ArgumentException>(() => new ReviewCandidateFile("x.json", "not-a-hash", 1, ReviewCandidateDocumentState.Draft));

            var file = new ReviewCandidateFile("x.json", new string('d', 64), 1, ReviewCandidateDocumentState.Draft);
            Assert.Throws<ArgumentException>(() => new DataReviewCandidate(
                "candidate-3",
                "duplicate",
                0,
                "branch",
                new string('e', 40),
                new[] { file, file }));
        }
    }
}
