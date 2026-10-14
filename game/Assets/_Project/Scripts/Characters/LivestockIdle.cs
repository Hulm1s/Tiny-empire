using Tycoon.UI;
using UnityEngine;

namespace Tycoon.Characters
{
    /// <summary>
    /// Procedural idle life for the pen animals - no bones, no Animator: the whole model is nudged
    /// a little on top of the pose it was built in. Chickens peck and hop, cows chew, swing their
    /// head and shift a step, pigs wiggle and snuffle.
    ///
    /// Cheap on purpose. Each animal rests for a random few seconds and then plays one short
    /// action; between actions it only breathes (a hair of vertical scale). Whether the animal is
    /// on screen is re-checked a couple of times a second with <see cref="WorldUi.IsVisible"/>,
    /// and an off-screen animal does nothing at all. Every animal starts at a random phase, so a
    /// pen never moves in unison. The base pose (position and rotation) is captured when the
    /// object is first enabled, so an animal revealed later by a purchase starts from where the
    /// builder put it and always returns to it.
    /// </summary>
    public sealed class LivestockIdle : MonoBehaviour
    {
        public enum Kind { Chicken, Cow, Pig }

        public Kind kind = Kind.Chicken;

        private Vector3 _basePos, _baseScale;
        private Quaternion _baseRot;
        private bool _captured;

        private float _nextAction;     // Time.time the next action starts
        private float _actionStart = -1f, _actionLength;
        private int _action;           // which action of the kind is playing
        private float _dir = 1f;       // random side for turns and steps
        private float _phase;          // per-animal breathing offset
        private float _nextVisCheck;
        private bool _visible = true;
        private Vector3 _shift;        // cow: current stepped offset (local), eased towards a target
        private Vector3 _shiftTarget;

        private void OnEnable()
        {
            if (!_captured)
            {
                _basePos = transform.localPosition;
                _baseRot = transform.localRotation;
                _baseScale = transform.localScale;
                _captured = true;
                _phase = Random.value * 10f;
            }
            _nextAction = Time.time + Random.Range(0.5f, 4f);
            _nextVisCheck = 0f;
            _actionStart = -1f;
        }

        private void OnDisable()
        {
            if (_captured) Apply(Vector3.zero, Vector3.zero, 0f);
        }

        private void Update()
        {
            float now = Time.time;
            if (now >= _nextVisCheck)
            {
                _nextVisCheck = now + 0.4f + Random.value * 0.2f;
                _visible = WorldUi.IsVisible(transform.position, 0.2f);
            }
            if (!_visible) return;

            Vector3 pos = Vector3.zero, rot = Vector3.zero;

            // Breathing.
            float breath = Mathf.Sin(now * 2.1f + _phase) * 0.012f;

            if (_actionStart < 0f && now >= _nextAction) StartAction(now);

            if (_actionStart >= 0f)
            {
                float t = (now - _actionStart) / _actionLength;
                if (t >= 1f)
                {
                    _actionStart = -1f;
                    _nextAction = now + Random.Range(2.0f, 6.0f);
                }
                else Pose(t, now, ref pos, ref rot);
            }

            if (kind == Kind.Cow)
            {
                // The stepped offset eases toward its target, so a shift stays shifted until the next one.
                _shift = Vector3.Lerp(_shift, _shiftTarget, 1f - Mathf.Exp(-2.5f * Time.deltaTime));
                pos += _shift;
                // Chewing: a quick tiny nod, always on.
                rot.x += Mathf.Sin(now * 9f + _phase) * 0.8f;
            }

            Apply(pos, rot, breath);
        }

        private void StartAction(float now)
        {
            _actionStart = now;
            _dir = Random.value < 0.5f ? -1f : 1f;
            switch (kind)
            {
                case Kind.Chicken:
                    _action = Random.value < 0.7f ? 0 : 1;               // 0 peck, 1 hop
                    _actionLength = _action == 0 ? Random.Range(1.0f, 1.6f) : 0.45f;
                    break;
                case Kind.Cow:
                    _action = Random.Range(0, 3);                        // 0 look round, 1 step, 2 shake
                    _actionLength = _action == 1 ? 1.2f : 2.2f;
                    if (_action == 1)
                    {
                        // A short step forward or back, kept inside the pen's slack.
                        var target = new Vector3(0f, 0f, _dir * 0.12f);
                        _shiftTarget = (_shiftTarget.z * _dir > 0.05f) ? Vector3.zero : target;
                    }
                    break;
                default:
                    _action = Random.value < 0.5f ? 0 : 1;               // 0 wiggle, 1 snuffle
                    _actionLength = _action == 0 ? 1.0f : 1.8f;
                    break;
            }
        }

        private void Pose(float t, float now, ref Vector3 pos, ref Vector3 rot)
        {
            float env = Mathf.Sin(t * Mathf.PI);   // 0 -> 1 -> 0 over the action
            switch (kind)
            {
                case Kind.Chicken:
                    if (_action == 0)
                    {
                        // Two or three pecks: nose down and back up.
                        float peck = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 3f));
                        rot.x = 38f * peck * Mathf.Min(1f, env * 3f);
                        pos.z = 0.03f * peck;
                    }
                    else
                    {
                        pos.y = 0.10f * Mathf.Sin(t * Mathf.PI);
                        rot.x = -8f * env;
                    }
                    break;

                case Kind.Cow:
                    if (_action == 0) rot.y = 16f * _dir * env;
                    else if (_action == 2) rot.z = Mathf.Sin(t * Mathf.PI * 4f) * 2.5f * env;
                    break;

                default:
                    if (_action == 0)
                    {
                        rot.y = Mathf.Sin(t * Mathf.PI * 7f) * 9f * env;
                        rot.z = Mathf.Sin(t * Mathf.PI * 7f + 1.2f) * 2f * env;
                    }
                    else
                    {
                        rot.x = (6f + Mathf.Sin(t * Mathf.PI * 9f) * 5f) * env;
                        pos.z = 0.02f * env;
                    }
                    break;
            }
        }

        private void Apply(Vector3 pos, Vector3 euler, float breath)
        {
            transform.localPosition = _basePos + _baseRot * pos;
            transform.localRotation = _baseRot * Quaternion.Euler(euler);
            transform.localScale = new Vector3(_baseScale.x, _baseScale.y * (1f + breath), _baseScale.z);
        }
    }
}
