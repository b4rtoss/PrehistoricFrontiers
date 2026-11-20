using UnityEngine;

namespace PrehistoricFrontiers
{
    public sealed class ConstructionSite : MonoBehaviour
    {
        public ConstructionSystem system;
        public StructureKind kind;
        public WorkerBuilder Builder;
        public float Progress { get; private set; }
        public bool Complete => Progress >= 1;
        public BuildingHealth Health => GetComponent<BuildingHealth>();
        GameObject body;
        Material material, accent;
        bool modelArt;
        public void CreateVisual()
        {
            var model = kind == StructureKind.Clinic ? system.clinicModelPrefab : system.barracksModelPrefab;
            if (model)
            {
                modelArt = true;
                body = Instantiate(model, transform);
                body.name = kind == StructureKind.Clinic ? "Clinic model" : "Barracks model";
                body.transform.localPosition = Vector3.zero;
                body.transform.localRotation = kind == StructureKind.Barracks && system.Team == UnitTeam.Player ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;
                body.transform.localScale = new Vector3(1, .18f, 1);
                body.GetComponent<ClinicVisual>()?.SetOperational(false);
                body.GetComponent<BarracksVisual>()?.SetOperational(false);
                return;
            }
            material = new Material(Shader.Find("Standard")) { color = new Color(.62f, .47f, .27f) };
            accent = new Material(Shader.Find("Standard")) { color = kind == StructureKind.Clinic ? new Color(.95f, .95f, .8f) : new Color(.5f, .22f, .12f) };
            body = Cube("Building", new Vector3(0, .3f, 0), new Vector3(4, .6f, 4), material);
        }
        GameObject Cube(string name, Vector3 position, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.layer = 13; go.transform.SetParent(transform, false);
            go.transform.localPosition = position; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = mat;
            Destroy(go.GetComponent<Collider>()); return go;
        }
        public void Work(float seconds)
        {
            if (system.controller.IsPaused || Complete || !Builder || !Health.IsAlive || system.controller.MatchOver) return;
            Progress = Mathf.Min(1, Progress + seconds / ConstructionSystem.Duration(kind));
            float height = .6f + Progress * 1.8f;
            if (modelArt) body.transform.localScale = new Vector3(1, .18f + Progress * .82f, 1);
            else { body.transform.localScale = new Vector3(4, height, 4); body.transform.localPosition = Vector3.up * height / 2; }
            if (!Complete) return;
            if (Health.team == UnitTeam.Player) system.controller.statistics?.RecordBuilt(kind);
            if (!modelArt) Cube("Roof", new Vector3(0, 2.55f, 0), new Vector3(4.3f, .3f, 4.3f), accent);
            if (kind == StructureKind.Barracks)
            {
                var production = gameObject.AddComponent<ProductionBuilding>(); production.controller = system.controller; production.healthBars = system.main.healthBars;
                production.wallet = system.main.wallet; production.unitPrefabs = new[] { system.main.unitPrefabs[0], system.main.unitPrefabs[1], system.main.unitPrefabs[2] };
                production.rallyPoint = transform.position + (Health.team == UnitTeam.Player ? Vector3.back : Vector3.forward) * 4.5f;
                system.ShowProduction(production);
                body.GetComponent<BarracksVisual>()?.SetOperational(true);
            }
            else
            {
                if (modelArt) body.GetComponent<ClinicVisual>()?.SetOperational(true);
                else
                {
                    material.color = new Color(.68f, .84f, .75f);
                    accent.color = new Color(.9f, .15f, .12f);
                    Cube("Medical cross horizontal", new Vector3(0, 2.85f, 0), new Vector3(1.8f, .15f, .5f), accent);
                    Cube("Medical cross vertical", new Vector3(0, 2.85f, 0), new Vector3(.5f, .15f, 1.8f), accent);
                }
                gameObject.AddComponent<HealingClinic>().controller = system.controller;
            }
            BattleEffects.Burst(BattleEffect.Completion, transform.position + Vector3.up * 3, 14);
            var worker = Builder; Builder = null; worker.Finish(this);
        }
        void OnDisable() { if (Builder) Builder.RemoveSite(this); }
        void OnDestroy() { if (material) Destroy(material); if (accent) Destroy(accent); }
    }
}
