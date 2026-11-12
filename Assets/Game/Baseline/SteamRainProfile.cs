#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Steamworks;
using UnityEngine;

namespace MTPSKIT
{
    internal sealed class SteamRainProfile : IDisposable
    {
        const string Achievement = "TPS_FIRST_CLEAR";
        const string CloudFile = "profiles/rain-progress-v1.json";
        const int MaxBytes = 4096;

        [Serializable]
        sealed class Snapshot
        {
            public int version;
            public string owner;
            public bool firstClear;
            public string[] monsterLogs;
        }

        readonly string root;
        readonly HashSet<string> monsterLogs = new HashSet<string>(StringComparer.Ordinal);
        string owner, path;
        bool firstClear, localSaved, cloudChecked, cloudSaved, achievementSaved, blocked;
        float cloudRetry, statsRetry, diskRetry;
        CallResult<RemoteStorageFileReadAsyncComplete_t> read;
        CallResult<RemoteStorageFileWriteAsyncComplete_t> write;
        Callback<UserStatsStored_t> stats;
        float cloudDeadline, statsDeadline;
        int revision, writingRevision;
        string cloudStatus = "本地档案", statsStatus = "";

        internal SteamRainProfile(string root) => this.root = root;
        internal bool FirstClear => firstClear;
        internal string Status => cloudStatus + statsStatus;
        internal bool HasMonsterLog(string id) => monsterLogs.Contains(id);

        internal void Tick(bool steamReady)
        {
            string steamOwner = steamReady ? SteamUser.GetSteamID().m_SteamID.ToString() : "guest";
            if (steamOwner == "0") steamOwner = "guest";
            if (owner != steamOwner) Open(steamOwner);
            float now = Time.realtimeSinceStartup;
            if (HasProgress && !localSaved && now >= diskRetry) SaveLocal();
            if (!steamReady || owner == "guest" || blocked) return;

            if (cloudDeadline > 0 && now >= cloudDeadline)
            {
                read?.Cancel();
                write?.Cancel();
                CloudRetry("云存档操作超时");
            }
            if (!cloudSaved && cloudDeadline == 0 && now >= cloudRetry && (!cloudChecked || localSaved)) SyncCloud();
            if (statsDeadline > 0 && now >= statsDeadline) StatsRetry("成就确认超时");
            if (firstClear && localSaved && !achievementSaved && statsDeadline == 0 && now >= statsRetry) SubmitAchievement();
        }

        internal void GrantFirstClear()
        {
            if (firstClear || blocked || path == null) return;
            firstClear = true;
            ProgressChanged();
        }

        internal void GrantMonsterLog(string id)
        {
            if (blocked || path == null || !IsMonsterLogId(id) || !monsterLogs.Add(id)) return;
            ProgressChanged();
        }

        bool HasProgress => firstClear || monsterLogs.Count != 0;

        void ProgressChanged()
        {
            revision++;
            localSaved = cloudSaved = false;
            cloudRetry = 0f;
            SaveLocal();
        }

        void Open(string nextOwner)
        {
            Dispose();
            owner = nextOwner;
            path = Path.Combine(root, SteamRoomSession.DevelopmentAppId.ToString(), owner, "rain-progress.json");
            firstClear = localSaved = cloudChecked = cloudSaved = achievementSaved = blocked = false;
            monsterLogs.Clear();
            revision = writingRevision = 0;
            cloudRetry = statsRetry = diskRetry = cloudDeadline = statsDeadline = 0;
            cloudStatus = "本地档案";
            statsStatus = "";
            try
            {
                if (!File.Exists(path)) return;
                var info = new FileInfo(path);
                if (info.Length > MaxBytes || !TryRead(File.ReadAllBytes(path), out var snapshot))
                    throw new InvalidDataException("档案版本、账号或内容不匹配");
                firstClear = snapshot.firstClear;
                monsterLogs.UnionWith(snapshot.monsterLogs ?? Array.Empty<string>());
                localSaved = HasProgress;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                blocked = true;
                cloudStatus = "本地档案待核对";
                Debug.LogWarning("RAIN_PROFILE_LOAD: " + e.Message);
            }
        }

        static bool IsMonsterLogId(string id) => !string.IsNullOrEmpty(id) &&
            id.Length <= 48 && id.StartsWith("Logs.", StringComparison.Ordinal) &&
            id.All(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_');

        bool TryRead(byte[] bytes, out Snapshot snapshot)
        {
            snapshot = null;
            try
            {
                snapshot = JsonUtility.FromJson<Snapshot>(Encoding.UTF8.GetString(bytes));
                return snapshot != null && snapshot.version == 1 && snapshot.owner == owner &&
                    (snapshot.monsterLogs == null || snapshot.monsterLogs.All(IsMonsterLogId));
            }
            catch (ArgumentException) { return false; }
        }

        byte[] SnapshotBytes() => Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Snapshot
        {
            version = 1, owner = owner, firstClear = firstClear,
            monsterLogs = monsterLogs.OrderBy(id => id, StringComparer.Ordinal).ToArray()
        }));

        void SaveLocal()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temporary = path + ".tmp";
                File.WriteAllBytes(temporary, SnapshotBytes());
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
                else File.Move(temporary, path);
                localSaved = true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                diskRetry = Time.realtimeSinceStartup + 10;
                Debug.LogWarning("RAIN_PROFILE_SAVE: " + e.Message);
            }
        }

        void SyncCloud()
        {
            if (!SteamRemoteStorage.IsCloudEnabledForAccount() || !SteamRemoteStorage.IsCloudEnabledForApp())
            {
                CloudRetry("Steam Cloud 未启用");
                return;
            }
            if (!cloudChecked && SteamRemoteStorage.FileExists(CloudFile))
            {
                int size = SteamRemoteStorage.GetFileSize(CloudFile);
                if (size <= 0 || size > MaxBytes) { CloudRetry("云档大小异常，已保留"); return; }
                var handle = SteamRemoteStorage.FileReadAsync(CloudFile, 0, (uint)size);
                if (handle == SteamAPICall_t.Invalid) { CloudRetry("云档读取待重试"); return; }
                read ??= CallResult<RemoteStorageFileReadAsyncComplete_t>.Create(OnRead);
                read.Set(handle);
                cloudDeadline = Time.realtimeSinceStartup + 30;
                return;
            }
            if (!localSaved)
            {
                cloudRetry = Time.realtimeSinceStartup + 30;
                return;
            }
            byte[] bytes = SnapshotBytes();
            var request = SteamRemoteStorage.FileWriteAsync(CloudFile, bytes, (uint)bytes.Length);
            if (request == SteamAPICall_t.Invalid) { CloudRetry("云档写入待重试"); return; }
            write ??= CallResult<RemoteStorageFileWriteAsyncComplete_t>.Create(OnWrite);
            writingRevision = revision;
            write.Set(request);
            cloudDeadline = Time.realtimeSinceStartup + 30;
        }

        void OnRead(RemoteStorageFileReadAsyncComplete_t result, bool ioFailure)
        {
            cloudDeadline = 0;
            if (!SameSteamOwner()) return;
            if (ioFailure || result.m_eResult != EResult.k_EResultOK || result.m_cubRead == 0 || result.m_cubRead > MaxBytes)
            { CloudRetry("云档读取待重试"); return; }
            var bytes = new byte[result.m_cubRead];
            if (!SteamRemoteStorage.FileReadAsyncComplete(result.m_hFileReadAsync, bytes, result.m_cubRead) ||
                !TryRead(bytes, out var snapshot))
            { CloudRetry("云档无法读取，已保留"); return; }
            cloudChecked = true;
            var cloudLogs = snapshot.monsterLogs ?? Array.Empty<string>();
            bool missingCloudProgress = firstClear && !snapshot.firstClear || !monsterLogs.IsSubsetOf(cloudLogs);
            bool gainedProgress = snapshot.firstClear && !firstClear || !monsterLogs.IsSupersetOf(cloudLogs);
            firstClear |= snapshot.firstClear;
            monsterLogs.UnionWith(cloudLogs);
            if (gainedProgress) ProgressChanged();
            cloudSaved = !missingCloudProgress;
            cloudStatus = "云档已读取";
        }

        void OnWrite(RemoteStorageFileWriteAsyncComplete_t result, bool ioFailure)
        {
            cloudDeadline = 0;
            if (!SameSteamOwner()) return;
            if (ioFailure || result.m_eResult != EResult.k_EResultOK) { CloudRetry("云档写入待重试"); return; }
            cloudSaved = writingRevision == revision;
            cloudStatus = "Steam 已接收云档";
        }

        bool SameSteamOwner() => owner == SteamUser.GetSteamID().m_SteamID.ToString();

        void CloudRetry(string message)
        {
            cloudDeadline = 0;
            cloudRetry = Time.realtimeSinceStartup + 30;
            cloudStatus = message;
        }

        void SubmitAchievement()
        {
            if (!SteamUserStats.GetAchievement(Achievement, out bool earned) ||
                (!earned && !SteamUserStats.SetAchievement(Achievement)))
            { StatsRetry("成就待提交，请核对 Steamworks 定义"); return; }
            stats ??= Callback<UserStatsStored_t>.Create(OnStatsStored);
            if (!SteamUserStats.StoreStats()) { StatsRetry("成就待提交"); return; }
            statsDeadline = Time.realtimeSinceStartup + 30;
            statsStatus = " · 成就待确认";
        }

        void OnStatsStored(UserStatsStored_t result)
        {
            if (statsDeadline == 0 || !SameSteamOwner() ||
                new CGameID(result.m_nGameID).AppID().m_AppId != SteamRoomSession.DevelopmentAppId) return;
            if (result.m_eResult != EResult.k_EResultOK ||
                !SteamUserStats.GetAchievement(Achievement, out bool earned) || !earned)
            { StatsRetry("成就待重试"); return; }
            statsDeadline = 0;
            achievementSaved = true;
            statsStatus = " · Steam 成就已确认";
        }

        void StatsRetry(string message)
        {
            statsDeadline = 0;
            statsRetry = Time.realtimeSinceStartup + 120;
            statsStatus = " · " + message;
        }

        public void Dispose()
        {
            read?.Dispose(); read = null;
            write?.Dispose(); write = null;
            stats?.Dispose(); stats = null;
        }
    }
}
#endif
