using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CupheadPort
{
    // Experiment only (CUPHEAD_MOVERS=1, off by default): measures what Box2D's handling of moving
    // static colliders costs. A 2D collider without a Rigidbody2D is static: moving it makes Unity
    // rebuild its shapes every physics step and before every query (Treetop Trouble moves ~60 such
    // colliders every frame). Here every 0.5 s the colliders without a body that moved get a
    // kinematic Rigidbody2D. NOT for play: it changes which trigger contacts exist (a static trigger
    // touches the game's kinematic bodies, a kinematic one does not), measured with the
    // test-trigger-pairs codepatch: AbstractCollidableObject.OnTriggerStay2D 2033/s -> 612/s.
    public static class Movers
    {
        static int on = -1;
        static float nextAt;
        static readonly Dictionary<int, Vector3> seen = new Dictionary<int, Vector3>();

        public static void Tick()
        {
            if (on < 0)
            {
                on = Environment.GetEnvironmentVariable("CUPHEAD_MOVERS") == "1" ? 1 : 0;
                if (on == 1) SceneManager.sceneLoaded += (s, m) => { seen.Clear(); nextAt = Time.realtimeSinceStartup + 1f; };
            }
            if (on == 0 || Time.realtimeSinceStartup < nextAt) return;
            nextAt = Time.realtimeSinceStartup + 0.5f;
            if (!SceneManager.GetActiveScene().name.StartsWith("scene_level")) return;
            foreach (Collider2D c in UnityEngine.Object.FindObjectsOfType<Collider2D>())
            {
                if (c.attachedRigidbody != null) continue;
                int id = c.GetInstanceID();
                Vector3 p = c.transform.position, before;
                bool moved = seen.TryGetValue(id, out before) && (before - p).sqrMagnitude > 1e-6f;
                seen[id] = p;
                if (!moved || c.GetComponentInChildren<LevelPlayerMotor>(true) != null) continue;
                Rigidbody2D rb = c.gameObject.AddComponent<Rigidbody2D>();
                if (rb == null) continue;
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.useFullKinematicContacts = false;
            }
        }
    }
}
