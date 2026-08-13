using UnityEngine;

namespace LastBeacon.Blockout
{
    /// <summary>
    /// Labelled scene gizmo for blockout landmarks (GDD Section 41, Rule 9).
    /// Marks where a system will live so the empty blockout stays readable
    /// before any real geometry or gameplay exists.
    /// </summary>
    public class BlockoutMarker : MonoBehaviour
    {
        // Appended only — existing values keep their serialized indices.
        public enum MarkerKind
        {
            Landmark,
            TaskStation,
            Entrance,
            DefenseSocket,
            SpawnPoint,
            Inspection,
            SystemControl
        }

        [SerializeField] MarkerKind kind = MarkerKind.Landmark;
        [SerializeField, TextArea] string note;
        [SerializeField] float radius = 0.6f;

        Color GizmoColor => kind switch
        {
            MarkerKind.TaskStation => new Color(1f, 0.75f, 0.2f),
            MarkerKind.Entrance => new Color(0.35f, 0.8f, 1f),
            MarkerKind.DefenseSocket => new Color(1f, 0.35f, 0.35f),
            MarkerKind.SpawnPoint => new Color(0.4f, 1f, 0.5f),
            MarkerKind.Inspection => new Color(0.85f, 0.5f, 1f),
            MarkerKind.SystemControl => new Color(0.5f, 1f, 0.9f),
            _ => new Color(0.85f, 0.85f, 0.85f)
        };

        void OnDrawGizmos()
        {
            Gizmos.color = GizmoColor;
            Gizmos.DrawWireSphere(transform.position, radius);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 2f);

#if UNITY_EDITOR
            var style = new GUIStyle
            {
                normal = { textColor = GizmoColor },
                fontSize = 11
            };
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2.2f,
                string.IsNullOrEmpty(note) ? name : $"{name}\n{note}",
                style);
#endif
        }
    }
}
