using UnityEngine;
using System.Collections;
using System.Collections.Generic;

using System.IO;
using System.Text;

#if (UNITY_WINRT && !UNITY_EDITOR) // WindowsStoreApps / WindowsPhone 8 ターゲットの時のみ有効.

// ファイル操作 [作成、取得、削除、存在判定].
using WinRTFile = UnityEngine.Windows.File;

// ディレクトリ取得 [install roaming temporary(cache?)]
using WinRTDirectory = UnityEngine.Windows.Directory;

// 暗号化ライブラリ [MD5 SHA1]
using WinRTCrypto = UnityEngine.Windows.Crypto;

#endif

public class WindowsFolderAccess : MonoBehaviour {

  public string persistentDataPath = "";
  public string installAppPath = "not found";
  public string roamingPath = "not found";
  public string tempPath = "not found";

  public GameCore.GameManager gameManager;

  void Awake () {
    gameManager = new GameCore.GameManager ();
  }

	// Use this for initialization
	void Start () {

    // アプリケーションディレクトリ取得テスト
    persistentDataPath = Application.persistentDataPath;

#if (UNITY_WINRT && !UNITY_EDITOR)
    // インストールフォルダ. ( ≈ Application.persistentDataPath)
    installAppPath = WinRTDirectory.localFolder;

    // ローミングデータフォルダ.
    roamingPath = WinRTDirectory.roamingFolder;

    // テンポラリデータフォルダ.
    tempPath = WinRTDirectory.temporaryFolder;
#endif
  }
	
  /*
	// Update is called once per frame
	void Update () {
	
	}
  */

  void OnGUI () {
    GUILayout.BeginVertical ();
    GUILayout.Label ("App Persistent Data Path:");
    GUILayout.TextField (persistentDataPath);
    GUILayout.Label ("Windows App Install Data Path:");
    GUILayout.TextField (installAppPath);
    GUILayout.Label ("Windows App Roaming Data Path:");
    GUILayout.TextField (roamingPath);
    GUILayout.Label ("Windows App Temporary Data Path:");
    GUILayout.TextField (tempPath);

    GUILayout.BeginHorizontal ();
    
    if (GUILayout.Button ("Load GameData")) {
      gameManager.GameLoad ();
    }
    if (GUILayout.Button ("Save GameData")) {
      gameManager.GameSave ();
    }
    if (GUILayout.Button ("Initialize GameData")) {
      gameManager.GameInitialize ();
    }

    GUILayout.EndHorizontal ();

    gameManager.GameDraw ();

    GUILayout.EndVertical ();
  }

}



namespace GameCore {

  /// <summary>
  /// 
  /// </summary>
  public class GameManager {
    const string FileName = "gamedata.bin";

    /// <summary>
    /// 
    /// </summary>
    public void GameSave () {
#if (UNITY_WINRT && !UNITY_EDITOR)
      using (var stream = new MemoryStream ()) {
        GameData.Instance.Write (stream);
        WinRTFile.WriteAllBytes (getGameFilePath (), stream.ToArray ());
      }
      Debug.Log ("Save Succeeded !");
#else
      Debug.Log ("Other Save Proc !!");
#endif
    }

    /// <summary>
    /// 
    /// </summary>
    public void GameLoad () {
      var path = getGameFilePath ();

#if (UNITY_WINRT  && !UNITY_EDITOR)
      if (WinRTFile.Exists (path) == false) {
        Debug.Log ("Not exists savedata. and Initialize game data");
        GameInitialize ();
        return;
      }

      var raw = WinRTFile.ReadAllBytes (getGameFilePath ());
      using (var stream = new MemoryStream (raw)) {
        try {
          GameData.Instance.Read (stream);
          Debug.Log ("Load Succeed !");

        } catch (System.Exception e) {  // データ読み込みに失敗したらログを出してデータ初期化を行う.
          Debug.LogWarning (e);
          Debug.Log ("Not exists savedata. and Initialize game data");
          GameInitialize ();
        }
      }
#else
      Debug.Log ("Other Load Proc !!");
      Debug.Log ("GameDataFile Path: " + path);
#endif
    }

    /// <summary>
    /// 
    /// </summary>
    public void GameInitialize () {
      var data = GameData.Instance;

      data.playerName = "Generated PlayerName";
      data.exp = Random.Range (100, 10000);
      data.objects.Clear ();
      var objCount = Random.Range(1, 100);
      for (int i = 0; i < objCount; i++) {
        data.objects.Add (new ObjectData (setStartType (), setStartPosition(), setStartRotation ()));
      }
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    private int setStartType () {
      return Random.Range (0, 10);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    private Vector3 setStartPosition () {
      return new Vector3 (Random.Range(-100f, 100f), Random.Range(-100f, 100f), Random.Range(-100f, 100f));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    private Quaternion setStartRotation () {
      return new Quaternion (Random.Range (-100f, 100f), Random.Range (-100f, 100f), Random.Range (-100f, 100f), Random.Range (-100f, 100f));
    }


    Vector2 scrollPos = Vector2.zero;
    /// <summary>
    /// 
    /// </summary>
    public void GameDraw () {
      scrollPos = GUILayout.BeginScrollView (scrollPos);

      GUILayout.Label (GameData.Instance.ToString ());

      GUILayout.EndScrollView ();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    private string getGameFilePath () {
      return Application.persistentDataPath + "/" + FileName;
    }
  }

  /// <summary>
  /// Game Data.
  /// </summary>
  public class GameData : IStorageFile {

    private static GameData _Instance;

    public const string ClientDataVersion = "1";

    static class PrefsKey {
      public const string PlayerName = "PlayerName";
      public const string Exp = "Exp";
      public const string DataVersion = "DataVersion";
    }

    /// <summary>
    /// 
    /// </summary>
    public static GameData Instance {
      get {
        if (_Instance == null) {
          _Instance = new GameData ();
        }
        return _Instance;
      }
    }

    public string playerName;
    public int exp;
    public List<ObjectData> objects;

    private delegate void DecompressAction (Stream raw);
    private IDictionary<string, DecompressAction> versionActionTable;

    /// <summary>
    /// 
    /// </summary>
    private GameData () {
      playerName = "Default Player";
      exp = 0;
      objects = new List<ObjectData> ();

      _Instance = this;

      versionActionTable = new Dictionary<string, DecompressAction> () {
        {"1", decompressSaveDataVersion1},
      };
    }

    /// <summary>
    /// ストリーム、PlayerPrefs からデータを読み込む.
    /// </summary>
    /// <param name="raw"></param>
    public void Read (Stream raw) {
      // データバージョンチェックを行いテーブルで指定したバージョンキーのデータ展開処理を行う.
      // データバージョン管理前のデータ構造で読み込みを試みる.

      try {
        if (tryGetDataFromSaveDataVersion (raw, versionActionTable) == false) {
          decompressSaveDataVersion0 (raw);
        }
      } catch (System.Exception e) {
        throw e;
      }
    }

    /// <summary>
    /// データバージョンを判定しデータの読み込みを試みる.
    /// </summary>
    /// <param name="actionTable"></param>
    /// <returns></returns>
    private bool tryGetDataFromSaveDataVersion (Stream raw, IDictionary<string, DecompressAction> actionTable) {
      // そもそも共有データ領域にバージョン情報が含まれていない場合は false
      if (PlayerPrefs.HasKey (PrefsKey.DataVersion) == false)
        return false;

      // データバージョンを取得.
      string saveDataVersion = PlayerPrefs.GetString (PrefsKey.DataVersion, "invalid");

      // 
      DecompressAction action;
      if (actionTable.TryGetValue (saveDataVersion, out action)) {
        try {
          action (raw);
        } catch (System.Exception e) {
          // 処理例外が発生したら、データ展開が失敗したとみなし false.
          Debug.LogWarning (e);
          return false;
        }
      } else {
        Debug.LogError ("Invalid client data version!! version string: " + saveDataVersion);
        return false;
      }

      return true;
    }

    /// <summary>
    /// データをストリームに書き込む.
    /// </summary>
    /// <param name="raw"></param>
    public void Write (Stream raw) {
      PlayerPrefs.SetString (PrefsKey.PlayerName, playerName);
      PlayerPrefs.SetInt (PrefsKey.Exp, exp);
      PlayerPrefs.SetString (PrefsKey.DataVersion, ClientDataVersion);

      var bin = new BinaryWriter (raw);
      bin.Write (objects.Count);

      foreach (var obj in objects) {
        obj.Write (bin.BaseStream);
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override string ToString () {
      var sb = new StringBuilder();

      sb.Append ("[GameData]").AppendLine ()
        .Append (" playerName: ").Append (playerName).AppendLine ()
        .Append (" exp: ").Append (exp).AppendLine ()
        .Append (" objects: ").Append (objects.Count).AppendLine ();

      foreach (var obj in objects) {
        sb.Append (obj).AppendLine();
      }

      return sb.ToString ();
    }


#region GameData decompress actions


    // Save data version 0.
    private void decompressSaveDataVersion0 (Stream raw) {

      try {
        // シーク位置をはじめに戻す.
        raw.Seek (0, SeekOrigin.Begin);

        Debug.Log ("Decompress Process Version 0");

        var bin = new BinaryReader (raw);

        playerName = bin.ReadString ();
        exp = bin.ReadInt32 ();

        int objCount = bin.ReadInt32 ();

        objects = new List<ObjectData> ();

        for (int count = 0; count < objCount; count++) {
          objects.Add (new ObjectData (bin.BaseStream));
        }
      } catch (System.Exception e) {
        throw CreateDecompressDataException ("0", e);
      }
    }

    // Save data version 1.
    private void decompressSaveDataVersion1(Stream raw) {
      try {
        Debug.Log ("Decompress Process Version 1");

        playerName = PlayerPrefs.GetString (PrefsKey.PlayerName, "Default PlayerName");
        PlayerPrefs.GetInt (PrefsKey.Exp);

        var bin = new BinaryReader (raw);

        int objCount = bin.ReadInt32 ();

        objects = new List<ObjectData> ();

        for (int count = 0; count < objCount; count++) {
          objects.Add (new ObjectData (bin.BaseStream));
        }
      } catch (System.Exception e) {  // 何らかの理由で展開に失敗したら例外を投げる.
        throw CreateDecompressDataException("1", e);
      }
    }

    private UnityException CreateDecompressDataException (string version, System.Exception innerException) {
      return new UnityException ("Decompress data failed! from version:" + version, innerException);
    }

#endregion
  }

  /// <summary>
  /// 
  /// </summary>
  public class ObjectData : IStorageFile {
    public int type;
    public Vector3 position;
    public Quaternion rotation;

    /// <summary>
    /// 
    /// </summary>
    public ObjectData () {
      this.type = -1;
      this.position = Vector3.zero;
      this.rotation = Quaternion.identity;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="type"></param>
    /// <param name="position"></param>
    /// <param name="rotation"></param>
    public ObjectData (int type, Vector3 position, Quaternion rotation) {
      this.type = type;
      this.position = position;
      this.rotation = rotation;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="raw"></param>
    public ObjectData (Stream raw) {
      Read (raw);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="raw"></param>
    public void Read (Stream raw) {
      var bin = new BinaryReader (raw);
      
      type = bin.ReadInt32();

      position.x = (float)bin.ReadDouble ();
      position.y = (float)bin.ReadDouble ();
      position.z = (float)bin.ReadDouble ();
      rotation.x = (float)bin.ReadDouble ();
      rotation.y = (float)bin.ReadDouble ();
      rotation.z = (float)bin.ReadDouble ();
      rotation.w = (float)bin.ReadDouble ();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="raw"></param>
    public void Write (Stream raw) {
      var bin = new BinaryWriter (raw);

      bin.Write (type);
      bin.Write ((double)position.x);
      bin.Write ((double)position.y);
      bin.Write ((double)position.z);
      bin.Write ((double)rotation.x);
      bin.Write ((double)rotation.y);
      bin.Write ((double)rotation.z);
      bin.Write ((double)rotation.w);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override string ToString () {
      var sb = new StringBuilder();

      sb.Append ("[Object Data]")
        .Append (" type: ").Append (type)
        .Append (" position: ").Append (position)
        .Append (" rotation: ").Append (rotation);

      return sb.ToString ();
    }
  }

  /// <summary>
  /// 
  /// </summary>
  public interface IStorageFile {
    void Read (Stream raw);
    void Write (Stream raw);
  }

  /// <summary>
  /// 
  /// </summary>
  public interface IPlayerPrefs {
    void ReadPrefs ();
    void WritePrefs ();
  }
}