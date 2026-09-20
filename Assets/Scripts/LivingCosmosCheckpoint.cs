using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace OrbitalRift
{
    [Serializable]
    public sealed class LivingCosmosCheckpoint
    {
        public const int CurrentVersion=1;
        public int version=CurrentVersion, seed, teamHealth, fireIntervalMilli, projectileSpeedMilli, damageBonus, prismLevel,
            aegisCharges, fieldRepairLevel, aegisLevel;
        public string runId, integrity;
        public LivingCosmosRunCheckpoint route;
        public TempoRewardCheckpoint tempo;
    }

    /// <summary>
    /// A narrow, local recovery point for the M4 reward choice. It is deliberately not
    /// a ranked/cloud save and only resumes a deterministic reward that is already fixed.
    /// </summary>
    public sealed class LivingCosmosCheckpointStore
    {
        private const string FileName="orbital-rift-living-cosmos-v1.json";
        private readonly string path, temporaryPath, backupPath;
        public LivingCosmosCheckpointStore(string directory=null)
        {
            var root=string.IsNullOrEmpty(directory) ? Application.persistentDataPath : directory;
            path=Path.Combine(root,FileName); temporaryPath=path+".tmp"; backupPath=path+".bak";
        }
        public bool HasCheckpoint { get { LivingCosmosCheckpoint ignored; return TryLoad(out ignored); } }
        public bool Save(LivingCosmosCheckpoint checkpoint)
        {
            if (!IsSane(checkpoint)) return false;
            try
            {
                var directory=Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                var json=Serialize(checkpoint);
                File.WriteAllText(temporaryPath,json,Encoding.UTF8);
                if (File.Exists(path)) File.Replace(temporaryPath,path,backupPath,true);
                else
                {
                    File.Move(temporaryPath,path);
                    File.Copy(path,backupPath,true);
                }
                return true;
            }
            catch { TryDelete(temporaryPath); return false; }
        }
        public bool TryLoad(out LivingCosmosCheckpoint checkpoint)
        {
            checkpoint=Read(path);
            if (checkpoint != null) return true;
            checkpoint=Read(backupPath);
            return checkpoint != null;
        }
        public void Clear()
        {
            TryDelete(path); TryDelete(temporaryPath); TryDelete(backupPath);
        }
        private static LivingCosmosCheckpoint Read(string file)
        {
            try
            {
                if (!File.Exists(file)) return null;
                var data=JsonUtility.FromJson<LivingCosmosCheckpoint>(File.ReadAllText(file,Encoding.UTF8));
                if (!IsSane(data)) return null;
                var integrity=data.integrity; data.integrity=string.Empty;
                return string.Equals(integrity,Hash(JsonUtility.ToJson(data)),StringComparison.Ordinal) ? data : null;
            }
            catch { return null; }
        }
        private static string Serialize(LivingCosmosCheckpoint data)
        {
            data.integrity=string.Empty;
            data.integrity=Hash(JsonUtility.ToJson(data));
            return JsonUtility.ToJson(data,true);
        }
        private static bool IsSane(LivingCosmosCheckpoint data)
        {
            return data != null && data.version==LivingCosmosCheckpoint.CurrentVersion && data.seed != 0 &&
                !string.IsNullOrWhiteSpace(data.runId) && data.route != null && data.tempo != null &&
                data.route.seed==data.seed && data.tempo.seed==data.seed && data.tempo.runId==data.runId &&
                data.teamHealth>0 && data.fireIntervalMilli>0 && data.projectileSpeedMilli>0 &&
                data.damageBonus>=0 && data.prismLevel>=0 && data.aegisCharges>=0 && data.fieldRepairLevel>=0 && data.aegisLevel>=0;
        }
        private static string Hash(string source)
        {
            unchecked
            {
                uint value=2166136261u;
                var bytes=Encoding.UTF8.GetBytes(source ?? string.Empty);
                for(var i=0;i<bytes.Length;i++) { value^=bytes[i]; value*=16777619u; }
                return value.ToString("X8");
            }
        }
        private static void TryDelete(string file) { try { if(File.Exists(file)) File.Delete(file); } catch { } }
    }
}
