using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Net;
using System.Windows.Media.Animation;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Windows.Input;
using MetroRadiance.UI;

namespace RandomSteamLibrary
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow
    {
        string steamFolder = "";
        string unplayedJudgmentTime = "";
        string steamWebAPIkey = "";
        string steamId = "";
        const string keyNmSteamFolder = "SteamFolder";
        const string keyNmOwnGameCount = "OwnGameCount";
        const string keyNmUnplayedJudgmentTime = "UnplayedJudgmentTime";
        const string keyNmSteamWebAPIkey = "SteamWebAPIkey";
        const string keyNmSteamId = "SteamId";
        string excFilePath = AppDomain.CurrentDomain.BaseDirectory + "exclusionConfig.xml";
        string wlFilePath = AppDomain.CurrentDomain.BaseDirectory + "wishlist.json";
        bool allModeFlg = false;
        bool unplayedModeFlg = false;
        bool wishModeFlg = false;
        List<int> numbersOrg = new List<int>();     // steamOwnedGamesのindex指定用(オリジナル)
        List<int> numbers = new List<int>();        // steamOwnedGamesのindex指定用
        List<int> numbersWishOrg = new List<int>(); // steamWishListのindex指定用(オリジナル)
        List<int> numbersWish = new List<int>();    // steamWishListのindex指定用
        int index = 0;
        bool allAppdtlGetFlg = false;    // ウィッシュリストの全てのゲームについてappdetailsで取得したらtrue
        List<string> tempExcIdList = new List<string>();

        SteamOwnedGames steamOwnedGames; // JSONの取得結果を格納(所持ゲーム)
        SteamWishList steamWishList;     // JSONの取得結果を格納(ウィッシュリスト)

        public MainWindow()
        {
            InitializeComponent();
            Init();
        }

        /// <summary>
        /// 設定ファイル読み込み、所持ゲーム一覧取得
        /// </summary>
        private void Init()
        {
            // App.configの読み込み
            steamFolder = System.Configuration.ConfigurationManager.AppSettings[keyNmSteamFolder].Trim();
            unplayedJudgmentTime = System.Configuration.ConfigurationManager.AppSettings[keyNmUnplayedJudgmentTime].Trim();
            steamWebAPIkey = System.Configuration.ConfigurationManager.AppSettings[keyNmSteamWebAPIkey].Trim();
            steamId = System.Configuration.ConfigurationManager.AppSettings[keyNmSteamId].Trim();

            // 必要な設定、ファイルに不足があるとアプリケーション終了
            ChkConfig(keyNmUnplayedJudgmentTime, unplayedJudgmentTime);

            ChkConfig(keyNmSteamWebAPIkey, steamWebAPIkey);

            ChkConfig(keyNmSteamId, steamId);

            // steamクライアントフォルダの存在チェック
            string path = steamFolder;

            if (path != "" && !System.IO.Directory.Exists(path)){
                MessageBox.Show("設定されているSteamクライアントフォルダが見つかりません。");
                Environment.Exit(1);
            }

            // 除外設定ファイルの存在チェック
            path = excFilePath;

            if (!System.IO.File.Exists(path)){
                MessageBox.Show("exclusionConfig.xmlが実行ファイルのフォルダに見つかりません。");
                Environment.Exit(1);
            }

            // コンボボックスの内容読み込み
            int count = 0;
            string exclusionSetting = "";
            List<string> lt = new List<string> { };

            try
            {
                count = int.Parse(System.Configuration.ConfigurationManager.AppSettings["ExclusionReasonMax"]);
            }
            catch (FormatException)
            {
                // 設定値が不適切な場合、0とする
                count = 0;
            }

            for(int i = 1; i <= count; i++)
            {
                exclusionSetting = System.Configuration.ConfigurationManager.AppSettings["ExclusionReason" + i];
                lt.Add(exclusionSetting);
            }

            // Listのnullチェック後、コンボボックスに設定
            if(lt?.Count > 0)
            {
                cmbReason.ItemsSource = lt;
            }

            // 所持ゲーム情報を取得
            WebApiAccess();

            // ウィッシュリスト情報を取得
            if (!GetWishList())
            {
                DispMessage("Failed to read wishlist.json file.");
            }

            // 除外リストを読み込む
            XElement xml = XElement.Load(excFilePath);
            IEnumerable<String> infos = from item in xml.Elements("exclusion").Elements("appId")
                                        select item.Value;

            RemoveExclusionGames(infos.ToList());
        }

        private void ChkConfig(string keyName, string readStr)
        {
            if (string.IsNullOrEmpty(readStr))
            {
                MessageBox.Show("configの" + keyName + "設定が不正です。");
                Environment.Exit(1);
            }

        }

        private  void WebApiAccess()
        {
            try
            {
                string url = "http://api.steampowered.com/IPlayerService/GetOwnedGames/v0001/?key="
                            + steamWebAPIkey + "&steamid=" + steamId + "&format=json&include_appinfo=1";

                WebRequest req = WebRequest.Create(url);
                WebResponse res = req.GetResponse();

                using (res)
                {
                    // レスポンス(JSON)をオブジェクトに変換
                    using (var resStream = res.GetResponseStream())
                    {
                        var serializer = new DataContractJsonSerializer(typeof(SteamOwnedGames));
                        steamOwnedGames = (SteamOwnedGames)serializer.ReadObject(resStream);
                    }
                }

                lblGameCount.Content = steamOwnedGames.response.game_count;

            }
            catch (Exception e)
            {
                MessageBox.Show("Error: " + e.ToString() + "\nWebAPI接続失敗: WebAPIキー、SteamIDの設定に間違いがないか確認してください。");
                this.Close();
            }
        }

        private void RemoveExclusionGames(List<string> excIdList)
        {
           
            if (excIdList.Count == 0) { return; }
            bool noGamesFlg = false;
            string noGamesText = "There are no games available for display.";

            // 除外するid以外、という形で抽出
            steamOwnedGames.response.games =
            steamOwnedGames.response.games.Where(game => excIdList.All(excId => game.appid != excId)).ToList();

            if(steamOwnedGames.response.games.Count == 0)
            {
                // 除外の結果所持ゲームを表示できなくなった場合
                // 一度しかこない想定
                DispMessage(noGamesText);
                btnAll.IsEnabled = false;
                btnUnplayed.IsEnabled = false;
                noGamesFlg = true;
            }

            numbersOrg = Enumerable.Range(0, steamOwnedGames.response.games.Count).ToList();
            lblGameCount.Content = steamOwnedGames.response.games.Count;

            if (steamWishList?.wishList.Count > 0)
            {
                steamWishList.wishList =
                steamWishList.wishList.Where(data => excIdList.All(excId => data.appid != excId)).ToList();

                if(steamWishList.wishList.Count == 0)
                {
                    btnWishList.IsEnabled = false;
                    if (!noGamesFlg) {
                        DispMessage(noGamesText);
                    }
                }

                numbersWishOrg = Enumerable.Range(0, steamWishList.wishList.Count).ToList();
                lblWishListCount.Content = steamWishList.wishList.Count;
            }

            tempExcIdList.Clear();
        }

        private int GetRandomIndex(List<int> numlist)
        {
            int ind = 0;
            Random random = new System.Random();
            bool skipFlg = false;

            // ランダム処理
            while (numlist.Count > 0)
            {
                skipFlg = false;
                ind = random.Next(0, numlist.Count);  // maxに指定した値は乱数範囲に含まない

                // 未プレイのゲームのみ表示する場合
                if (unplayedModeFlg)
                {
                    try
                    {
                        int jTime = int.Parse(unplayedJudgmentTime);
                        int playTime = int.Parse(steamOwnedGames.response.games[numlist[ind]].playtime_forever);
                        if (jTime < playTime){
                            skipFlg = true;
                            numlist.RemoveAt(ind);
                        }
                    }
                    catch(FormatException e)
                    {
                        MessageBox.Show("Error: " + e.ToString());
                        this.Close();
                    }
                }

                if (!skipFlg)
                {
                    break;
                }
            }
            return ind;
        }

        private void DispGameInfo(int ownIndex)
        {
            // Titleを表示
            lblTitle.Content = steamOwnedGames.response.games[ownIndex].name;
            lblAppIDVal.Content = steamOwnedGames.response.games[ownIndex].appid;

            // ローカルから画像ファイルを取得できればする。できなければWebから取得
            string path = steamFolder + @"\appcache\librarycache\"
                        + steamOwnedGames.response.games[ownIndex].appid + "_header.jpg";
            BitmapImage bmpImage = new BitmapImage();

            if (System.IO.File.Exists(path))
            {
                bmpImage = new BitmapImage();
                using (FileStream stream = File.OpenRead(path))
                {
                    bmpImage.BeginInit();
                    bmpImage.StreamSource = stream;
                    bmpImage.CacheOption = BitmapCacheOption.OnLoad;
                    bmpImage.CreateOptions = BitmapCreateOptions.None;
                    bmpImage.EndInit();
                    bmpImage.Freeze();
                }

                imgAppHeader.Source = bmpImage;
            }
            else
            {

                bmpImage = new BitmapImage(new Uri(@"https://cdn.cloudflare.steamstatic.com/steam/apps/"
                                            + steamOwnedGames.response.games[ownIndex].appid + "/header.jpg"));
                imgAppHeader.Source = bmpImage;
            }

            btnExclusion.IsEnabled = true;
        }

        private bool GetWishList()
        {
            try
            {
                if (!System.IO.File.Exists(wlFilePath))
                {
                    // ファイルがない場合は何もせず終了
                    return true;
                }

                // ファイルあれば読み込む
                var json = File.ReadAllText(wlFilePath);
                json = json.Replace(Environment.NewLine, "");
                json = json.TrimEnd(';');
                StringBuilder sb = new StringBuilder();
                sb.Append(@"{""wishlist"":");
                sb.Append(json);
                sb.Append("}");

                using (StringReader reader = new StringReader(sb.ToString()))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    steamWishList = (SteamWishList)serializer.Deserialize(reader, typeof(SteamWishList));
                }

                if(steamWishList?.wishList.Count > 0)
                {
                    lblWishListCount.Content = steamWishList.wishList.Count;
                    btnWishList.IsEnabled = true;
                    return true;
                }
              
                return false;
              
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void AppDtlWebApiAccess(int wlIndex)
        {
            try
            {
                if (allAppdtlGetFlg) { return; }
                string aId = steamWishList.wishList[wlIndex].appid;
                string url = "https://store.steampowered.com/api/appdetails?appids="
                            + aId + "&cc=jp&filters=basic,price_overview,genres";

                WebRequest req = WebRequest.Create(url);
                WebResponse res = req.GetResponse();

                using (res)
                {
                    // レスポンス(JSON)をオブジェクトに変換
                    using (var resStream = res.GetResponseStream())
                    {
                        Encoding encoding = Encoding.GetEncoding("utf-8");
                        StreamReader readStream = new StreamReader(resStream, encoding);

                        steamWishList.wishList[wlIndex].name = "";
                        steamWishList.wishList[wlIndex].final_formatted = "";
                        steamWishList.wishList[wlIndex].supported_languages = "";
                        steamWishList.wishList[wlIndex].description = new List<string>();

                        var obj = JObject.Parse(readStream.ReadToEnd());

                        var val = ((JValue)(obj.SelectToken(@"$." + aId + ".success"))).Value;
                        if ((bool)val)
                        {
                            steamWishList.wishList[wlIndex].name = ((JValue)(obj.SelectToken(@"$." + aId + ".data.name"))).Value.ToString();

                            if((obj.SelectToken(@"$." + aId + ".data.price_overview.final_formatted")) != null)
                            {
                                string finalFormatted = ((JValue)(obj.SelectToken(@"$." + aId + ".data.price_overview.final_formatted"))).Value.ToString();
                                string discountStr = ((JValue)(obj.SelectToken(@"$." + aId + ".data.price_overview.discount_percent"))).Value.ToString();
                                steamWishList.wishList[wlIndex].final_formatted = finalFormatted;
                                if (int.TryParse(discountStr, out int discountInt))
                                {
                                    if (discountInt > 0)
                                    {
                                        steamWishList.wishList[wlIndex].final_formatted = finalFormatted + "(-" + discountStr + "%)";
                                    }
                                }

                            }

                            if (((JValue)(obj.SelectToken(@"$." + aId + ".data.supported_languages"))).Value.ToString().Contains("日本語") ||
                                ((JValue)(obj.SelectToken(@"$." + aId + ".data.supported_languages"))).Value.ToString().Contains("Japanese"))
                            {
                                steamWishList.wishList[wlIndex].supported_languages = "日本語対応あり";
                            }
                            else
                            {
                                steamWishList.wishList[wlIndex].supported_languages = "";
                            }

                            foreach (var item in obj.SelectTokens(@"$." + aId + ".data.genres..description"))
                            {
                                steamWishList.wishList[wlIndex].description.Add(((JValue)item).Value.ToString());
                            }
                        }
                    }
                }

            }
            catch (Exception e)
            {
                MessageBox.Show("Error: " + e.ToString() + "\nWebAPI接続失敗: appdetailsが取得できませんでした。");
                this.Close();
            }
        }

        private void DispGameInfoWish(int wlIndex)
        {
            // Titleを表示
            lblTitle.Content = steamWishList.wishList[wlIndex].name;
            lblAppIDVal.Content = steamWishList.wishList[wlIndex].appid;
            lblJapanese.Content = steamWishList.wishList[wlIndex].supported_languages;
            lblPrice.Content = steamWishList.wishList[wlIndex].final_formatted;

            BitmapImage bmpImage = new BitmapImage();
            bmpImage = new BitmapImage(new Uri(@"https://cdn.cloudflare.steamstatic.com/steam/apps/"
                                        + steamWishList.wishList[wlIndex].appid + "/header.jpg"));
            imgAppHeader.Source = bmpImage;

            btnExclusion.IsEnabled = true;
        }

        private void DispMessage(string text)
        {
            lblMessage.Content = text;
            Storyboard storyboard = (Storyboard)this.lblMessage.Resources["fadeStoryboard"];
            storyboard.Begin();
        }

        /// <summary>
        /// All Games クリック
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnAll_Click(object sender, RoutedEventArgs e)
        {
            btnAll.IsEnabled = false;
            try
            {
                // 初回押下時か、他のランダムボタン押下後にこちらを押した場合
                if (!allModeFlg)
                {
                    allModeFlg = true;
                    unplayedModeFlg = false;
                    wishModeFlg = false;
                    lblJapanese.Content = "";
                    lblPrice.Content = "";

                    RemoveExclusionGames(tempExcIdList);

                    if(steamOwnedGames.response.games.Count != 0)
                    {
                        numbers = new List<int>(numbersOrg);
                        index = GetRandomIndex(numbers);
                        DispGameInfo(numbers[index]);
                        lblCurrentGameCount.Content = numbers.Count;
                    }
                }
                // 連続で押した場合
                else
                {
                    numbers.RemoveAt(index);
                    index = GetRandomIndex(numbers);

                    // 除外対象ではない所持ゲームを全て表示した場合
                    if (numbers.Count == 0)
                    {
                        // numbersリストをリセットする
                        RemoveExclusionGames(tempExcIdList);
                        numbers = new List<int>(numbersOrg);
                        DispMessage("All games displayed. Restart random display.");
                    }
                    else
                    {
                        DispGameInfo(numbers[index]);
                    }
                    lblCurrentGameCount.Content = numbers.Count;
                }
            }
            finally
            {
                if (steamOwnedGames.response.games.Count != 0)
                {
                    btnAll.IsEnabled = true;
                }
            }
        }

        private void btnUnplayed_Click(object sender, RoutedEventArgs e)
        {
            btnUnplayed.IsEnabled = false;
            try
            {
                // 初回押下時か、他のランダムボタン押下後にこちらを押した場合
                if (!unplayedModeFlg)
                {
                    allModeFlg = false;
                    unplayedModeFlg = true;
                    wishModeFlg = false;
                    lblJapanese.Content = "";
                    lblPrice.Content = "";

                    RemoveExclusionGames(tempExcIdList);
                    
                    if(steamOwnedGames.response.games.Count != 0)
                    {
                        numbers = new List<int>(numbersOrg);
                        index = GetRandomIndex(numbers);
                        DispGameInfo(numbers[index]);
                        lblCurrentGameCount.Content = numbers.Count;
                    }
                }
                // 連続で押した場合
                else
                {
                    numbers.RemoveAt(index);
                    index = GetRandomIndex(numbers);

                    // 除外対象ではない所持ゲームを全て表示した場合
                    if (numbers.Count == 0)
                    {
                        // numbersリストをリセットする
                        RemoveExclusionGames(tempExcIdList);
                        numbers = new List<int>(numbersOrg);
                        DispMessage("All games displayed. Restart random display.");
                    }
                    else
                    {
                        DispGameInfo(numbers[index]);
                    }
                    lblCurrentGameCount.Content = numbers.Count;
                }
            }
            finally
            {
                if (steamOwnedGames.response.games.Count != 0)
                {
                    btnUnplayed.IsEnabled = true;
                }
            }
        }

        private void btnExclusion_Click(object sender, RoutedEventArgs e)
        {
            // 重複で書き込まないよう、クリック後は別のボタン押すまで押下不可にする
            btnExclusion.IsEnabled = false;

            try
            {
                // 除外リストを読み込む
                XElement xml = XElement.Load(excFilePath);

                string cmbReasonVal = "";
                if (cmbReason.SelectedItem == null)
                {
                    cmbReasonVal = "";
                }
                else
                {
                    cmbReasonVal = cmbReason.SelectedItem.ToString();
                }

                // 除外リストに現在表示しているゲームを追加する
                XElement datas = new XElement("exclusion",
                new XElement("appId", lblAppIDVal.Content),
                new XElement("appTitle", lblTitle.Content),
                new XElement("exclusionReason", cmbReasonVal));
         
                xml.Add(datas);
                xml.Save(excFilePath);
                tempExcIdList.Add(lblAppIDVal.Content.ToString());
            }
            catch (Exception)
            {
                DispMessage("Failed to write exclusionConfig.xml");
            }

        }

        private void btnWishList_Click(object sender, RoutedEventArgs e)
        {
            btnWishList.IsEnabled = false;
            try
            {
                // 初回押下時か、他のランダムボタン押下後にこちらを押した場合
                if (!wishModeFlg)
                {
                    allModeFlg = false;
                    unplayedModeFlg = false;
                    wishModeFlg = true;

                    RemoveExclusionGames(tempExcIdList);

                    if (steamWishList.wishList.Count != 0)
                    {
                        numbersWish = new List<int>(numbersWishOrg);
                        index = GetRandomIndex(numbersWish);
                        AppDtlWebApiAccess(numbersWish[index]);
                        DispGameInfoWish(numbersWish[index]);
                        lblCurrentWishListCount.Content = numbersWish.Count;
                    }

                }
                // 連続で押した場合
                else
                {
                    numbersWish.RemoveAt(index);
                    index = GetRandomIndex(numbersWish);

                    // 除外対象ではない所持ゲームを全て表示した場合
                    if (numbersWish.Count == 0)
                    {
                        // numbersWishリストをリセットする
                        allAppdtlGetFlg = true;
                        RemoveExclusionGames(tempExcIdList);
                        numbersWish = new List<int>(numbersWishOrg);
                        DispMessage("All games displayed. Restart random display.");
                    }
                    else
                    {
                        AppDtlWebApiAccess(numbersWish[index]);
                        DispGameInfoWish(numbersWish[index]);
                    }
                    lblCurrentWishListCount.Content = numbersWish.Count;
                }
            }
            finally
            {
                if(steamWishList.wishList.Count != 0)
                {
                    btnWishList.IsEnabled = true;
                }
            }

        }

        private void imgAppHeader_MouseDown(object sender, MouseEventArgs e)
        {
            // 画像クリックでストアページを開く
            System.Diagnostics.Process.Start("https://store.steampowered.com/app/" + lblAppIDVal.Content);
        }

        private void lblAppIDVal_MouseDown(object sender, MouseEventArgs e)
        {
            // AppIDをクリックでクリップボードにコピー
            Clipboard.SetDataObject(lblAppIDVal.Content, true);
            DispMessage("Copy AppID to clipboard.");
        }

        private void lblTitle_MouseDown(object sender, MouseEventArgs e)
        {
            // ゲームタイトルをクリックでクリップボードにコピー
            Clipboard.SetDataObject(lblTitle.Content, true);
            DispMessage("Copy Title to clipboard.");
        }

    }

    // JSONで受け取った情報の入れ物
    [DataContract]
    public class SteamOwnedGames
    {
        [DataMember]
        public Response response { get; set; }
    }

    [DataContract]
    public class Response
    {
        [DataMember]
        public string game_count { get; set; }
        [DataMember]
        public List<game> games { get; set; }

        [DataContract]
        public class game
        {
            [DataMember]
            public string appid { get; set; }
            [DataMember]
            public string name { get; set; }
            [DataMember]
            public string playtime_forever { get; set; }
            [DataMember]
            public string img_icon_url { get; set; }
            [DataMember]
            public string has_community_visible_stats { get; set; }
            [DataMember]
            public string playtime_windows_forever { get; set; }
            [DataMember]
            public string playtime_mac_forever { get; set; }
            [DataMember]
            public string playtime_linux_forever { get; set; }
            [DataMember]
            public string rtime_last_played { get; set; }
            [DataMember]
            public List<int> content_descriptorids { get; set; }
        }
    }


    [DataContract]
    public class SteamWishList
    {
        [DataMember]
        public List<WishListData> wishList { get; set; }
    }

    [DataContract]
    public class WishListData
    {
        [DataMember]
        public string appid { get; set; }
        [DataMember]
        public string priority { get; set; }
        [DataMember]
        public string added { get; set; }
        [DataMember]
        public string name { get; set; }
        [DataMember]
        public string final_formatted { get; set; }
        [DataMember]
        public string supported_languages { get; set; }
        [DataMember]
        public List<string> description { get; set; }

    }

}
