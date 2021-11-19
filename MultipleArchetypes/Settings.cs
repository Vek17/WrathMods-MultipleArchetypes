using UnityModManagerNet;

namespace MultipleArchetypes {
    public class Settings : UnityModManager.ModSettings {
        public bool MultiArchetype = true;

        public override void Save(UnityModManager.ModEntry modEntry) {
            Save(this, modEntry);
        }
    }
}
