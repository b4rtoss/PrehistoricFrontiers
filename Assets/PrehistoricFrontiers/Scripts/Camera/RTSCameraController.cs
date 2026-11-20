using UnityEngine;

namespace PrehistoricFrontiers
{
    [RequireComponent(typeof(Camera))]
    public sealed class RTSCameraController : MonoBehaviour
    {
        public float moveSpeed = 12, zoomSpeed = 8, minZoom = 5, maxZoom = 18;
        Camera cam;
        Vector3 dragAnchor;
        bool dragging;
        readonly Plane ground = new Plane(Vector3.up, Vector3.zero);
        void Awake() { cam = GetComponent<Camera>(); }
        void Update()
        {
            if (MatchResultsScreen.IsVisible) return;
            var forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            var right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            var movement = Vector3.ClampMagnitude(right * Input.GetAxisRaw("Horizontal") + forward * Input.GetAxisRaw("Vertical"), 1);
            transform.position += movement * moveSpeed * Time.unscaledDeltaTime;
            if (!BattleHUD.IsOverPanel(Input.mousePosition))
                cam.orthographicSize = Mathf.Clamp(cam.orthographicSize - Input.GetAxis("Mouse ScrollWheel") * zoomSpeed, minZoom, maxZoom);
            if (Input.GetMouseButtonDown(2)) dragging = !BattleHUD.IsOverPanel(Input.mousePosition) && PointOnGround(out dragAnchor);
            if (Input.GetMouseButtonUp(2)) dragging = false;
            if (dragging && Input.GetMouseButton(2) && PointOnGround(out var current))
                transform.position += dragAnchor - current;
            ClampPosition();
            if (Input.GetKeyDown(KeyCode.Home)) ResetView();
        }
        bool PointOnGround(out Vector3 point)
        {
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            bool found = ground.Raycast(ray, out float distance);
            point = ray.GetPoint(distance);
            return found;
        }
        public void ClampPosition()
        {
            var p = transform.position;
            p.x = Mathf.Clamp(p.x, -22, 22);
            p.z = Mathf.Clamp(p.z, -34, 10);
            p.y = 12;
            transform.position = p;
        }
        public void ResetView()
        {
            transform.position = new Vector3(0, 12, -14);
            cam.orthographicSize = 14;
        }
        public void FocusOn(Vector3 position)
        {
            float distance = -12 / transform.forward.y;
            var offset = transform.forward * distance;
            transform.position = new Vector3(position.x - offset.x, 12, position.z - offset.z);
            ClampPosition();
        }
    }
}
