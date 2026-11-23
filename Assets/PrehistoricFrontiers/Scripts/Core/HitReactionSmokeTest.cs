using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class HitReactionSmokeTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-hitReactionSmoke") >= 0)
                new GameObject("Hit reaction regression").AddComponent<HitReactionSmokeTest>();
        }
        bool failed;
        string report = "";
        void Check(bool result, string name)
        {
            string line = (result ? "PASS " : "FAIL ") + name;
            failed |= !result; report += line + "\n"; Debug.Log(line); File.WriteAllText("hitreaction-test.txt", report);
        }
        IEnumerator Start()
        {
            yield return null;
            var c = FindAnyObjectByType<RTSSelectionController>();
            SmokeTestSetup.DisableEnemyProduction(c, false);
            c.SetAutoBattle(false); c.SetAutoScout(false); c.SetWorkerProtection(false);
            foreach (var u in c.units) u.Stop();
            for (int i = 0; i < c.enemies.Length; i++)
            {
                var u = c.enemies[i]; u.Stop();
                u.Agent.Warp(new Vector3(15 + (i % 2) * 6, 0, 15 + (i / 2) * 6));
            }
            yield return null;
            var all = new System.Collections.Generic.List<RTSUnit>(c.units); all.AddRange(c.enemies);
            foreach (var u in all)
            {
                var visual = UnitHitReaction.FindModel(u);
                var position = visual.localPosition; var rotation = visual.localRotation;
                var root = u.transform.position;
                var hp = u.GetComponent<UnitHealth>(); var reaction = u.GetComponent<UnitHitReaction>();
                float health = hp.CurrentHealth;
                var renderer = visual.GetComponentInChildren<Renderer>();
                var originalMaterial = renderer.sharedMaterial;
                Check(renderer.enabled && !visual.GetComponentInChildren<Collider>(), u.name + " reaction targets visible art without gameplay colliders");
                hp.TakeDamage(1, u.team == UnitTeam.Player ? UnitTeam.Enemy : UnitTeam.Player, root + Vector3.right * 2);
                Check(reaction && reaction.HitCount == 1 && reaction.IsFlashing && hp.CurrentHealth == health - 1,
                    u.name + " receives flash with unchanged damage");
                var colorBlock = new MaterialPropertyBlock(); renderer.GetPropertyBlock(colorBlock, 0);
                Check(colorBlock.HasColor("_Color") && colorBlock.GetColor("_Color") != originalMaterial.color,
                    u.name + " visible renderer actually changes color");
                yield return new WaitForSeconds(.07f);
                Check(Vector3.Distance(visual.localPosition, position) > .005f && Vector3.Distance(u.transform.position, root) < .01f,
                    u.name + " recoils visually without moving navigation root");
                Check(renderer.sharedMaterial == originalMaterial, u.name + " keeps shared material");
                yield return new WaitForSeconds(.4f);
                Check(reaction.Remaining == 0 && Vector3.Distance(visual.localPosition, position) < .001f
                    && Quaternion.Angle(visual.localRotation, rotation) < .01f, u.name + " returns to original pose");
                renderer.GetPropertyBlock(colorBlock, 0);
                Check(!colorBlock.HasColor("_Color"), u.name + " clears flash color override");
            }
            var fighter = c.units[0]; var feedback = fighter.GetComponent<UnitHitReaction>();
            var healthComponent = fighter.GetComponent<UnitHealth>(); var model = UnitHitReaction.FindModel(fighter);
            var restPose = model.localPosition;
            int count = feedback.HitCount;
            healthComponent.TakeDamage(0, UnitTeam.Enemy); healthComponent.TakeDamage(float.NaN, UnitTeam.Enemy);
            healthComponent.Heal(1); healthComponent.TakeDamage(1, UnitTeam.Player); healthComponent.TakeDamage(1);
            Check(feedback.HitCount == count, "healing invalid damage and nonhostile damage do not trigger hostile feedback");
            healthComponent.TakeDamage(1, UnitTeam.Enemy, fighter.transform.position + Vector3.forward);
            yield return new WaitForSeconds(.07f);
            c.SetPaused(true); float remaining = feedback.Remaining; var pose = model.localPosition;
            yield return new WaitForSecondsRealtime(.3f);
            Check(feedback.Remaining == remaining && model.localPosition == pose, "pause freezes recoil and flash");
            c.SetPaused(false); yield return new WaitForSeconds(.4f);
            Check(feedback.Remaining == 0, "resume completes reaction");
            for (int i = 0; i < 3; i++) { healthComponent.TakeDamage(1, UnitTeam.Enemy); yield return new WaitForSeconds(.05f); }
            yield return new WaitForSeconds(.4f);
            Check(feedback.Remaining == 0 && Vector3.Distance(model.localPosition, restPose) < .001f, "repeated hits leave no accumulated recoil");
            // Actual combat must deliver the reaction through the same damage path.
            var enemy = c.enemies[0]; fighter.Agent.Warp(new Vector3(-6, 0, -12)); enemy.Agent.Warp(new Vector3(-4, 0, -12));
            count = feedback.HitCount; enemy.GetComponent<UnitCombat>().Attack(fighter);
            float end = Time.time + 6;
            while (feedback.HitCount == count && Time.time < end) yield return null;
            enemy.Stop();
            Check(feedback.HitCount > count, "real enemy melee attack triggers reaction");
            yield return new WaitForSeconds(.4f);
            // Capture the four friendly models during their reaction for visual review.
            for (int i = 0; i < c.units.Length; i++) { c.units[i].Stop(); c.units[i].Agent.Warp(new Vector3(-12 + i * 4, 0, -5)); }
            var cam = c.sceneCamera; cam.GetComponent<RTSCameraController>().enabled = false;
            cam.transform.position = new Vector3(-6, 15, -21); cam.transform.LookAt(new Vector3(-6, 0, -5)); cam.orthographicSize = 9;
            foreach (var u in c.units) u.GetComponent<UnitHealth>().TakeDamage(1, UnitTeam.Enemy, u.transform.position + Vector3.forward * 2);
            yield return new WaitForSeconds(.055f);
            var rt = new RenderTexture(1440, 900, 24); var tex = new Texture2D(1440, 900, TextureFormat.RGB24, false);
            var old = cam.targetTexture; var active = RenderTexture.active;
            try { cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0); tex.Apply(); File.WriteAllBytes("hit-reaction-preview.png", tex.EncodeToPNG()); }
            finally { cam.targetTexture = old; RenderTexture.active = active; rt.Release(); Destroy(rt); Destroy(tex); }
            yield return new WaitForSeconds(.4f);
            c.fog.SetFogEnabled(true); c.fog.ResetExploration(); c.fog.RefreshNow();
            var hidden = c.enemies[1]; hidden.Agent.Warp(new Vector3(24, 0, 24)); c.fog.RefreshNow();
            var hiddenReaction = hidden.GetComponent<UnitHitReaction>(); count = hiddenReaction.HitCount;
            hidden.GetComponent<UnitHealth>().TakeDamage(1, UnitTeam.Player);
            Check(!c.CanSee(UnitTeam.Player, hidden.transform.position) && hiddenReaction.HitCount == count, "fog hides enemy hit feedback");
            healthComponent.TakeDamage(10000, UnitTeam.Enemy);
            Check(!fighter.IsAlive && feedback.Remaining == 0, "death clears visual reaction");
            Application.Quit(failed ? 1 : 0);
        }
    }
}
