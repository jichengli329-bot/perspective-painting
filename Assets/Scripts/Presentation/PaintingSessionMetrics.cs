using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PerspectivePuzzle.Presentation
{
    /// <summary>Anonymous offline-only playtest counters. No upload or device identifiers.</summary>
    public sealed class PaintingSessionMetrics : MonoBehaviour
    {
        [Serializable] public sealed class PieceCounter { public string piece; public int pickups; public int releases; public int invalidReleases; }
        [Serializable] public sealed class Snapshot
        {
            public string gallery;
            public float elapsedSeconds;
            public bool completed;
            public int undoUses, resetUses, hintUses, assistUses, compareUses, inspectUses;
            public List<PieceCounter> pieces = new List<PieceCounter>();
        }

        [SerializeField] private PaintingManipulationController _manipulation;
        [SerializeField] private PaintingCompletionReveal _reveal;
        [SerializeField] private string _gallery = "Mist Valley";
        private readonly Dictionary<PaintingManipulablePiece, PieceCounter> _counts = new();
        private float _startedAt;
        private bool _saved;
        private bool _tabHeld, _spaceHeld;
        public Snapshot Latest { get; private set; }
        public string OutputPath => Path.Combine(Application.persistentDataPath, "perspective-painting-last-session.json");

        private void Awake() { _startedAt = Time.realtimeSinceStartup; }
        private void OnEnable()
        {
            if (_manipulation != null)
            {
                _manipulation.PlacementStarted += OnStarted;
                _manipulation.PlacementReleased += OnReleased;
                _manipulation.UndoPerformed += OnUndo;
                _manipulation.ResetPerformed += OnReset;
                _manipulation.AssistPerformed += OnAssist;
            }
            if (_reveal != null) _reveal.RevealCompleted += OnCompleted;
        }
        private void OnDisable() { Unsubscribe(); if (!_saved) Save(false); }
        private void OnApplicationQuit() { if (!_saved) Save(false); }
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.H)) EnsureLatest().hintUses++;
            if (Input.GetKeyDown(KeyCode.G)) EnsureLatest().assistUses++;
            if (Input.GetKeyDown(KeyCode.Tab) && !_tabHeld) { EnsureLatest().compareUses++; _tabHeld = true; }
            if (Input.GetKeyUp(KeyCode.Tab)) _tabHeld = false;
            if (Input.GetKeyDown(KeyCode.Space) && !_spaceHeld) { EnsureLatest().inspectUses++; _spaceHeld = true; }
            if (Input.GetKeyUp(KeyCode.Space)) _spaceHeld = false;
        }
        private Snapshot EnsureLatest() => Latest ??= new Snapshot { gallery = _gallery };
        private PieceCounter Counter(PaintingManipulablePiece piece)
        {
            if (!_counts.TryGetValue(piece, out PieceCounter counter))
            {
                counter = new PieceCounter { piece = piece != null ? piece.Root.name : "Unknown" };
                _counts.Add(piece, counter); EnsureLatest().pieces.Add(counter);
            }
            return counter;
        }
        private void OnStarted(PaintingManipulablePiece piece) => Counter(piece).pickups++;
        private void OnReleased(PaintingManipulablePiece piece, bool valid) { var c = Counter(piece); c.releases++; if (!valid) c.invalidReleases++; }
        private void OnUndo() => EnsureLatest().undoUses++;
        private void OnReset() => EnsureLatest().resetUses++;
        private void OnAssist(PaintingManipulablePiece piece) { /* G is counted from input; public assist remains neutral. */ }
        private void OnCompleted() => Save(true);
        public void Save(bool completed)
        {
            Snapshot snapshot = EnsureLatest();
            snapshot.elapsedSeconds = Mathf.Max(0f, Time.realtimeSinceStartup - _startedAt);
            snapshot.completed = completed;
            try { File.WriteAllText(OutputPath, JsonUtility.ToJson(snapshot, true)); _saved = completed; }
            catch (Exception exception) { Debug.LogWarning("Local playtest metrics could not be saved: " + exception.Message); }
        }
        private void Unsubscribe()
        {
            if (_manipulation != null) { _manipulation.PlacementStarted -= OnStarted; _manipulation.PlacementReleased -= OnReleased; _manipulation.UndoPerformed -= OnUndo; _manipulation.ResetPerformed -= OnReset; _manipulation.AssistPerformed -= OnAssist; }
            if (_reveal != null) _reveal.RevealCompleted -= OnCompleted;
        }
    }
}
