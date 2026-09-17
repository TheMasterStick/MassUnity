using System;
using System.Collections.Generic;

namespace MassRPG.Data.Publishing
{
    public enum ReviewCandidateDocumentState
    {
        Draft,
        ReadyForReview
    }

    public sealed class ReviewCandidateFile
    {
        public ReviewCandidateFile(string path, string sha256, long bytes, ReviewCandidateDocumentState state)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Candidate file path is required.", nameof(path));
            if (!IsLowerHex(sha256, 64)) throw new ArgumentException("Candidate file SHA-256 must be 64 lowercase hexadecimal characters.", nameof(sha256));
            if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
            Path = path;
            Sha256 = sha256;
            Bytes = bytes;
            State = state;
        }

        public string Path { get; }
        public string Sha256 { get; }
        public long Bytes { get; }
        public ReviewCandidateDocumentState State { get; }
        public bool IsReadyForReview => State == ReviewCandidateDocumentState.ReadyForReview;

        internal static bool IsLowerHex(string value, int requiredLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length != requiredLength) return false;
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Immutable review/local-test snapshot of repository-backed draft data. A candidate points at
    /// an exact Git commit and hashes every included draft file. It is intentionally one stage below
    /// PublishedDataManifest: a candidate can be reviewed/tested but cannot itself activate live data.
    /// Generic "Other Game Definition" records are design-authoring documents until promoted to a
    /// dedicated typed runtime schema, and are therefore an explicit publishing gate.
    /// </summary>
    public sealed class DataReviewCandidate
    {
        private readonly List<ReviewCandidateFile> _files;
        private readonly Dictionary<string, int> _documentCounts;
        private readonly Dictionary<string, int> _readyCounts;

        public DataReviewCandidate(
            string candidateId,
            string label,
            long createdUnixMilliseconds,
            string sourceBranch,
            string sourceCommit,
            IEnumerable<ReviewCandidateFile> files,
            IReadOnlyDictionary<string, int> documentCounts = null,
            IReadOnlyDictionary<string, int> readyCounts = null,
            int designOnlyDefinitionCount = 0)
        {
            if (string.IsNullOrWhiteSpace(candidateId)) throw new ArgumentException("Candidate id is required.", nameof(candidateId));
            if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("Candidate label is required.", nameof(label));
            if (createdUnixMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(createdUnixMilliseconds));
            if (string.IsNullOrWhiteSpace(sourceBranch)) throw new ArgumentException("Source branch is required.", nameof(sourceBranch));
            if (!ReviewCandidateFile.IsLowerHex(sourceCommit, 40)) throw new ArgumentException("Source commit must be a full 40-character lowercase Git SHA-1.", nameof(sourceCommit));
            if (files == null) throw new ArgumentNullException(nameof(files));
            if (designOnlyDefinitionCount < 0) throw new ArgumentOutOfRangeException(nameof(designOnlyDefinitionCount));

            CandidateId = candidateId;
            Label = label;
            CreatedUnixMilliseconds = createdUnixMilliseconds;
            SourceBranch = sourceBranch;
            SourceCommit = sourceCommit;
            DesignOnlyDefinitionCount = designOnlyDefinitionCount;
            _files = new List<ReviewCandidateFile>(files);
            if (_files.Count == 0) throw new ArgumentException("A review candidate must contain at least one draft file.", nameof(files));

            var paths = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < _files.Count; i++)
            {
                if (_files[i] == null) throw new ArgumentException("Candidate files cannot contain null entries.", nameof(files));
                if (!paths.Add(_files[i].Path)) throw new ArgumentException("Candidate contains duplicate file path '" + _files[i].Path + "'.", nameof(files));
            }

            _documentCounts = CopyCounts(documentCounts);
            _readyCounts = CopyCounts(readyCounts);
        }

        public string CandidateId { get; }
        public string Label { get; }
        public long CreatedUnixMilliseconds { get; }
        public string SourceBranch { get; }
        public string SourceCommit { get; }
        public IReadOnlyList<ReviewCandidateFile> Files => _files;
        public IReadOnlyDictionary<string, int> DocumentCounts => _documentCounts;
        public IReadOnlyDictionary<string, int> ReadyCounts => _readyCounts;
        public int DesignOnlyDefinitionCount { get; }
        public int FileCount => _files.Count;
        public bool ContainsDesignOnlyDefinitions => DesignOnlyDefinitionCount > 0;

        public bool AllDraftsReady
        {
            get
            {
                for (var i = 0; i < _files.Count; i++)
                    if (!_files[i].IsReadyForReview) return false;
                return true;
            }
        }

        /// <summary>
        /// This only means the candidate has reached the human-review readiness gate. Actual live
        /// promotion must still pass authoritative content validation/package hashing/versioning.
        /// Generic design-only definitions deliberately make the candidate ineligible until those
        /// records have been promoted into dedicated typed content.
        /// </summary>
        public bool IsEligibleForPublishReview => AllDraftsReady && !ContainsDesignOnlyDefinitions;

        public PublishedDataManifest BeginPublishedManifest(
            PublishedDataVersion version,
            long createdUnixMilliseconds,
            string label,
            PublishedDataVersion? parentVersion = null,
            int schemaVersion = 1)
        {
            if (!AllDraftsReady)
                throw new InvalidOperationException("Every candidate document must be ReadyForReview before a published manifest can be started.");
            if (ContainsDesignOnlyDefinitions)
                throw new InvalidOperationException("Review candidates containing generic design-only definitions cannot begin live publication. Promote those definitions to dedicated typed content first.");
            return new PublishedDataManifest(version, createdUnixMilliseconds, label, parentVersion, schemaVersion);
        }

        private static Dictionary<string, int> CopyCounts(IReadOnlyDictionary<string, int> source)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            if (source == null) return result;
            foreach (var pair in source)
            {
                if (string.IsNullOrWhiteSpace(pair.Key)) throw new ArgumentException("Candidate count category cannot be empty.", nameof(source));
                if (pair.Value < 0) throw new ArgumentOutOfRangeException(nameof(source), "Candidate counts cannot be negative.");
                result.Add(pair.Key, pair.Value);
            }
            return result;
        }
    }
}
