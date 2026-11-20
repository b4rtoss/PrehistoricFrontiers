using UnityEngine;
namespace PrehistoricFrontiers
{
    public enum EnvironmentArtKind { Deciduous, Conifer, Rock, BerryBush }
    public sealed class EnvironmentArt : MonoBehaviour
    {
        public EnvironmentArtKind kind;
        public int variant;
        public bool resource;
    }
}
