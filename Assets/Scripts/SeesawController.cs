using UnityEngine;

namespace KnockoutBalance
{
    [RequireComponent(typeof(Rigidbody2D), typeof(HingeJoint2D))]
    public sealed class SeesawController : MonoBehaviour
    {
        [SerializeField] private float halfLength = 6f;
        [SerializeField] private float maximumAngularSpeed = 75f;
        [SerializeField] private float levelingStrength = 6f;
        [SerializeField] private float levelingDamping = 1.5f;
        [SerializeField] private float maximumLevelingTorque = 55f;
        private Rigidbody2D body;
        private Vector2 startingPosition;
        private float startingRotation;
        private bool initialized;

        public float HalfLength => halfLength;
        public float Angle
        {
            get
            {
                EnsureInitialized();
                return Mathf.DeltaAngle(0f, body.rotation);
            }
        }

        public float AngularVelocity
        {
            get
            {
                EnsureInitialized();
                return body.angularVelocity;
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (initialized) return;
            body = GetComponent<Rigidbody2D>();
            if (body == null) return;
            startingPosition = body.position;
            startingRotation = body.rotation;
            initialized = true;
        }

        private void FixedUpdate()
        {
            EnsureInitialized();
            if (body == null) return;
            float angleFromLevel = Mathf.DeltaAngle(startingRotation, body.rotation);
            float levelingTorque = -angleFromLevel * levelingStrength - body.angularVelocity * levelingDamping;
            body.AddTorque(Mathf.Clamp(levelingTorque, -maximumLevelingTorque, maximumLevelingTorque), ForceMode2D.Force);
            body.angularVelocity = Mathf.Clamp(body.angularVelocity, -maximumAngularSpeed, maximumAngularSpeed);
        }

        public Vector2 LocalPointToWorld(float localX) => transform.TransformPoint(new Vector3(localX, 0f, 0f));

        public void ConfigureHalfLength(float value)
        {
            halfLength = Mathf.Max(0.1f, value);
        }

        public void ResetPhysics()
        {
            EnsureInitialized();
            if (body == null)
            {
                Debug.LogError("SeesawController requires a Rigidbody2D before it can reset physics.", this);
                return;
            }
            body.position = startingPosition;
            body.rotation = startingRotation;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
    }
}
