using UnityEngine;

namespace LastBeacon.Blockout
{
    /// <summary>
    /// PLACEHOLDER — replaced by BeaconController in Phase 4.
    ///
    /// Rotates the lantern-room spotlight so the blockout can be judged under a
    /// sweeping beam (workflow doc, Phase 1 step 4). No modes, no power, no heat.
    /// </summary>
    public class BlockoutBeaconSpinner : MonoBehaviour
    {
        [SerializeField] float degreesPerSecond = 24f;

        void Update()
        {
            transform.Rotate(Vector3.up, degreesPerSecond * Time.deltaTime, Space.World);
        }
    }
}
