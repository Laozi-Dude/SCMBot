﻿using System;
using System.ComponentModel;
using System.Net;
using System.Windows.Forms;
using System.Threading;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace SCMBot
{
    partial class SteamSite
    {
        public string UserName { set; get; }
        public string Password { set; get; }

        public int scanID { set; get; }
        public int mainDelay { get; set; }
        public int resellDelay { get; set; }

        public string linkTxt { set; get; }
   
        public bool Logged { set; get; }
        public bool LoginProcess { set; get; }
        public bool scaninProg { set; get; }

        public bool BuyNow { set; get; }
        public bool LoadOnSale { get; set; }
        public bool isRemove { get; set; }

        public bool IgnoreWarn { get; set; }

        public int invApp { get; set; }

        public bool NotSetHead { get; set; }

        public static string myUserId;

        public CookieContainer cookieCont { set; get; }

        public saveTabLst recentInputList { set; get; }
        public BindingList<MainScanItem.LogItem> logContainer = new BindingList<MainScanItem.LogItem>();


        public saveTab scanInput { set; get; }

        public Semaphore Sem = new Semaphore(0, 1);

        private BackgroundWorker loginThread = new BackgroundWorker();
        private BackgroundWorker scanThread = new BackgroundWorker();
        private BackgroundWorker listedThread = new BackgroundWorker();
        private BackgroundWorker reqThread = new BackgroundWorker();
        private BackgroundWorker sellThread = new BackgroundWorker();

        private BackgroundWorker getInventory = new BackgroundWorker();

        public List<ItemToSell> toSellList = new List<ItemToSell>();
        public List<string> toUnSellList = new List<string>();

        public CurrInfoLst currencies = new CurrInfoLst();

        public SteamSite()
        {
            loginThread.WorkerSupportsCancellation = true;
            loginThread.DoWork += new DoWorkEventHandler(loginThread_DoWork);

            scanThread.WorkerSupportsCancellation = true;
            scanThread.DoWork += new DoWorkEventHandler(scanThread_DoWork);

            listedThread.WorkerSupportsCancellation = true;
            listedThread.DoWork += new DoWorkEventHandler(listedThread_DoWork);

            reqThread.WorkerSupportsCancellation = true;
            reqThread.DoWork += new DoWorkEventHandler(reqThread_DoWork);


            getInventory.WorkerSupportsCancellation = true;
            getInventory.DoWork += new DoWorkEventHandler(getInventory_DoWork);

            sellThread.WorkerSupportsCancellation = true;
            sellThread.DoWork += new DoWorkEventHandler(sellThread_DoWork);
         }

        public class AppType
        {
            public AppType(string app, string context)
            {
                this.App = app;
                this.Context = context;
            }
            public string App { set; get; }
            public string Context { set; get; }
        }

        public class ItemToSell
        {
            public ItemToSell(string assetid, string price)
            {
                this.AssetId = assetid;
                this.Price = price;
            }

            public string AssetId { set; get; }
            public string Price { set; get; }
        }

        public void Login()
        {
            if (loginThread.IsBusy != true)
            {
                loginThread.RunWorkerAsync();
            }
        }

        public void CancelLogin()
        {
            if (loginThread.IsBusy == true)
            {
                loginThread.CancelAsync();
            }
        }

        public void Logout()
        {
            ThreadStart threadStart = delegate() {
                SendGet(_logout, cookieCont);
                doMessage(flag.Logout_, 0, string.Empty, true);
                Logged = false;
            };
            Thread pTh = new Thread(threadStart);
            pTh.IsBackground = true;
            pTh.Start();
        }

        public void ChangeLng(string lang)
        {
            ThreadStart threadStart = delegate()
            {
                SendGet(_lang_chg + lang, cookieCont);
                doMessage(flag.Lang_Changed, 0, lang, true);
            };
            Thread pTh = new Thread(threadStart);
            pTh.IsBackground = true;
            pTh.Start();
        }


        public void ScanPrices()
        {
            if (scanThread.IsBusy != true)
            {
                scaninProg = true;
                scanThread.RunWorkerAsync();
            }
        }

        public void CancelScan()
        {
            if (scanThread.WorkerSupportsCancellation == true && scanThread.IsBusy)
            {
                scaninProg = false;
                Sem.Release();
                scanThread.CancelAsync();
            }
        }



        internal void ScanNewListed()
        {
            if (listedThread.IsBusy != true)
            {
                scaninProg = true;
                listedThread.RunWorkerAsync();
            }
        }

        public void CancelListed()
        {
            if (listedThread.WorkerSupportsCancellation == true && listedThread.IsBusy)
            {
                scaninProg = false;
                Sem.Release();
                listedThread.CancelAsync();
            }
        }


        public void reqLoad()
        {
            if (reqThread.IsBusy != true)
            {
                reqThread.RunWorkerAsync();
            }
        }


        public void loadInventory()
        {
            if (getInventory.IsBusy != true)
            {
                getInventory.RunWorkerAsync();
            }
        }

        public void ItemSell()
        {
            if (sellThread.IsBusy != true)
            {
                sellThread.RunWorkerAsync();
            }
        }


        public void GetPriceTread(string url, int pos, bool isInv)
        {
            ThreadStart threadStart = delegate()
            {
                
                var tempLst = new List<ScanItem>();

                ParseLotList(SendGet(UrlForRender(url), cookieCont), tempLst, currencies, false);


                if (tempLst.Count != 0)
                {
                    var fl = flag.ActPrice;

                    if (isInv)
                        fl = flag.InvPrice;

                    doMessage(fl, pos, tempLst[0].Price.ToString(), true);
                }
            };
            Thread pTh = new Thread(threadStart);
            pTh.IsBackground = true;
            pTh.Start();

        }


 


        private void getInventory_DoWork(object sender, DoWorkEventArgs e)
        {
            int invCount = 0;
            if (!LoadOnSale)
            {
                invCount = ParseInventory(SendGet(string.Format(_jsonInv, myUserId, GetUrlApp(invApp, true).App),
              cookieCont));
            }
            else
            {
                invCount = ParseOnSale(SendGet(_market, cookieCont), currencies);
            }

            if (invCount > 0)
                doMessage(flag.Inventory_Loaded, 0, string.Empty, true);
            else
                doMessage(flag.Inventory_Loaded, 1, string.Empty, true);

        }


        private void sellThread_DoWork(object sender, DoWorkEventArgs e)
        {
            var cunt = toSellList.Count;

            if (cunt != 0)
            {
                var appReq = GetUrlApp(invApp, false);

                int incr = (100 / cunt);

                for (int i = 0; i < cunt; i++)
                {
                    if (isRemove)
                    {
                        var req = "sessionid=" + GetSessId(cookieCont);
                        SendPost(req, removeSell + toSellList[i].AssetId, _market, false);
                    }
                    else
                    {
                        if (toSellList[i].Price != Strings.None)
                        {

                            var req = string.Format(sellReq, GetSessId(cookieCont), appReq.App, appReq.Context, toSellList[i].AssetId, toSellList[i].Price);

                            SendPost(req, _sellitem, _market, false);
                        }
                    }

                    doMessage(flag.Sell_progress, 0, (incr * (i + 1)).ToString(), true);
                }

                doMessage(flag.Items_Sold, 0, string.Empty, true);
            }
            else
            {
                doMessage(flag.Items_Sold, 1, string.Empty, true);
            }
        }


        private void reqThread_DoWork(object sender, DoWorkEventArgs e)
        {

            doMessage(flag.Search_success, 0, ParseSearchRes(SendGet(linkTxt, cookieCont), searchList, currencies), true);
        }


        private RespRSA GetRSA()
        {
           return JsonConvert.DeserializeObject<RespRSA>(SendPost("username=" + UserName, _getrsa, _ref, true));
        }

        private void LoginProgr(string value)
        {
            doMessage(flag.Rep_progress, 0, value, true);
        }


        private void loginThread_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;

            LoginProcess = true;
            Logged = false;

            LoginProgr("10");

            string accInfo = GetNameBalance(cookieCont, currencies);

            if (accInfo != string.Empty)
            {
                doMessage(flag.Already_logged, 0, accInfo, true);
                doMessage(flag.Rep_progress, 0, "100", true);
                LoginProcess = false;
                Logged = true;
                return;
            }

            string mailCode = string.Empty;
            string guardDesc = string.Empty;
            string capchaId = string.Empty;
            string capchaTxt = string.Empty;
            string mailId = string.Empty;


            //Login cycle
        begin:


            if (worker.CancellationPending == true)
                return;

            LoginProgr("20");

            var rRSA = GetRSA();

            if (rRSA == null)
            {
                Main.AddtoLog("Network Problem");
                doMessage(flag.Login_cancel, 0, "Network Problem", true);
                e.Cancel = true;
                LoginProcess = false;
                return;
            }


            LoginProgr("40");

            string finalpass = EncryptPassword(Password, rRSA.Module, rRSA.Exponent);

            string MainReq = string.Format(loginReq, finalpass, UserName, mailCode, guardDesc, capchaId,
                                                                          capchaTxt, mailId, rRSA.TimeStamp);
            string BodyResp = SendPost(MainReq, _dologin, _ref, true);

            LoginProgr("60");

            //Checking login problem
            if (BodyResp.Contains("message"))
            {
                var rProcess = JsonConvert.DeserializeObject<RespProcess>(BodyResp);

                //Checking Incorrect Login
                if (rProcess.Message == "Incorrect login")
                {
                    Main.AddtoLog("Incorrect login");
                    doMessage(flag.Login_cancel, 0, "Incorrect login", true);
                    e.Cancel = true;
                    LoginProcess = false;
                    return;
                }
                else
                {
                    //Login correct, checking message type...

                    Dialog guardCheckForm = new Dialog();

                    if (rProcess.isCaptcha)
                    {
                        //Verifying humanity, loading capcha
                        guardCheckForm.capchgroupEnab = true;
                        guardCheckForm.codgroupEnab = false;

                        string newcap = _capcha + rProcess.Captcha_Id;
                        Main.loadImg(newcap, guardCheckForm.capchImg, false, false);
                    }
                    else
                        if (rProcess.isEmail)
                        {
                            //Steam guard wants email code
                            guardCheckForm.capchgroupEnab = false;
                            guardCheckForm.codgroupEnab = true;
                        }
                        else
                        {
                            //Whoops!
                            goto begin;
                        }

                    //Re-assign main request values
                    if (guardCheckForm.ShowDialog() == DialogResult.OK)
                    {
                        mailCode = guardCheckForm.MailCode;
                        guardDesc = guardCheckForm.GuardDesc;
                        capchaId = rProcess.Captcha_Id;
                        capchaTxt = guardCheckForm.capchaText;
                        mailId = rProcess.Email_Id;
                        guardCheckForm.Dispose();
                    }
                    else
                    {
                        Main.AddtoLog("Dialog has been cancelled");
                        doMessage(flag.Login_cancel, 0, "Dialog has been cancelled", true);
                        e.Cancel = true;
                        Logged = false;
                        LoginProcess = false;
                        guardCheckForm.Dispose();
                        return;

                    }

                    goto begin;
                }

            }
            else
            {
                //No Messages, Success!
                var rFinal = JsonConvert.DeserializeObject<RespFinal>(BodyResp);

                LoginProgr("80");

                if (rFinal.Success && rFinal.isComplete)
                {
                    //Okay
                    string accInfo2 = GetNameBalance(cookieCont, currencies);
                    doMessage(flag.Login_success, 0, accInfo2, true);

                    LoginProgr("100");

                    Logged = true;
                    Main.AddtoLog("Login Success");
                }
                else
                {
                    //Fail
                    goto begin;
                }

            }

            LoginProcess = false;
        }



        public int BuyLogic(int wished, string sessid, ScanItem ourItem, saveTab Input, int buyCont, bool ismain)
        {
            int total = ourItem.Price + ourItem.Fee;
            string totalStr = total.ToString();

            if (total <= wished)
            {
                if (Input.ToBuy)
                {

                    var buyresp = BuyItem(cookieCont, sessid, ourItem.ListringId, Input.Link, ourItem.Price.ToString(), ourItem.Fee.ToString(), totalStr);

                    if (buyresp.Succsess)
                    {
                        //Resell 
                        if (Input.ResellType != 0)
                        {
                            StartResellThread(totalStr, Input.ResellPrice, ourItem.Type, ourItem.ItemName, Input.ResellType);
                        }

                        doMessage(flag.Success_buy, scanID, buyresp.Mess, ismain);
                        doMessage(flag.Price_btext, scanID, totalStr, ismain);

                        buyCont++;

                        //Bloody hell, you're fuckin' genious!
                        if (buyCont == Input.BuyQnt)
                        {
                            Input.ToBuy = false;
                            doMessage(flag.Send_cancel, scanID, string.Empty, ismain);
                        }

                        return buyCont;

                    }

                    else
                    {
                        doMessage(flag.Error_buy, scanID, buyresp.Mess, ismain);
                    }
 

                }
                else doMessage(flag.Price_htext, scanID, totalStr, ismain);
            }
            else
                doMessage(flag.Price_text, scanID, totalStr, ismain);

            return buyCont;
        }


        public bool fillLotList(string link, bool full, bool ismain)
        {
            if (!isStillLogged(cookieCont))
            {
                doMessage(flag.ReLogin, scanID, string.Empty, true);
                doMessage(flag.Error_scan, scanID, "4", ismain);
                return false;
            }
            else
            {
                lotList.Clear();
                byte ret = ParseLotList(SendGet(link, cookieCont), lotList, currencies, full);

                if (ret != 5)
                {
                    doMessage(flag.Error_scan, scanID, ret.ToString(), ismain);
                    return false;
                }
                else return true;
            }
        }


        public string UrlForRender(string input)
        {
            string url = input;

            int fint = url.IndexOf('?');

            if (fint == -1)
            {
                url += "/render/";
            }
            else
            {
                url = url.Insert(fint, "/render/");
            }
            return url;
        }

        public void scanThread_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                BackgroundWorker worker = sender as BackgroundWorker;

                string sessid = GetSessId(cookieCont);
                string url = UrlForRender(scanInput.Link);

                if (BuyNow)
                {
                    ParseLotList(SendGet(url, cookieCont), lotList, currencies, false);

                    if (lotList.Count == 0)
                    {
                        doMessage(flag.Error_scan, scanID, "0", true);
                    }
                    else
                    {
                        string totalStr = Convert.ToString(lotList[0].Price + lotList[0].Fee);
                        var buyresp = BuyItem(cookieCont, sessid, lotList[0].ListringId, scanInput.Link, lotList[0].Price.ToString(), lotList[0].Fee.ToString(), totalStr);

                        BuyNow = false;
                        if (buyresp.Succsess)
                        {
                            doMessage(flag.Success_buy, scanID, buyresp.Mess, true);
                        }
                        else
                        {
                            doMessage(flag.Error_buy, scanID, buyresp.Mess, true);
                        }
                    }
                    return;
                }


                int buyCounter = 0;

                //Scan cycle
                while (worker.CancellationPending == false)
                {
                    try
                    {
                        if (fillLotList(url, false, true))
                            buyCounter = BuyLogic(Convert.ToInt32(GetSweetPrice(scanInput.Price)), sessid, lotList[0], scanInput, buyCounter, true);

                    }
                    catch (Exception exc)
                    {
                        Main.AddtoLog(exc.Message);
                    }
                    finally
                    {
                        doMessage(flag.Scan_progress, scanID, string.Empty, true);
                        Sem.WaitOne(scanInput.Delay);
                    }
                }
            }
            catch (Exception exc)
            {
                Main.AddtoLog(exc.Message);
            }

            finally
            {
                doMessage(flag.Scan_cancel, scanID, string.Empty, true);
                CancelScan();
            }

        }


        public void listedThread_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            string sessid = GetSessId(cookieCont);
           
            int[] buyCounter = new int[recentInputList.Count];

            for (int i = 0; i < buyCounter.Length; i++)
            {
                buyCounter[i] = 0; 
            }


            //Scan cycle
            while (worker.CancellationPending == false)
            {
                bool found = false;

                try
                {
                    if (fillLotList(recentMarket, true, false))
                    {

                        for (int i = 0; i < lotList.Count; i++)
                        {
                            for (int k = 0; k < recentInputList.Count; k++)
                            {
                                if (recentInputList[k].StatId == status.InProcess)
                                {
                                    if (lotList[i].ItemName == recentInputList[k].Name)
                                    {
                                        found = true;
                                        scanID = k;
                                        buyCounter[k] = BuyLogic(Convert.ToInt32(GetSweetPrice(recentInputList[k].Price)), sessid, lotList[i], recentInputList[k], buyCounter[k], false);
                                        continue;
                                    }
                                }
                            }

                        }

                    }
                }
                catch (Exception exc)
                {
                    Main.AddtoLog(exc.Message);
                }
                finally
                {
                    if (!found)
                        doMessage(flag.Price_text, 1, "Not found", false);
                    doMessage(flag.Scan_progress, scanID, string.Empty, false);
                    Sem.WaitOne(mainDelay);
                }

            }

            doMessage(flag.Scan_cancel, scanID, string.Empty, false);
        }


        private void StartResellThread(string lotPrice, string resellPrice, AppType appType, string markName, int resellType)
        {
            ThreadStart threadStart = delegate()
            {
                try
                {
                    if (resellDelay > 0)
                        Thread.Sleep(resellDelay);

                    int sellPrice = Convert.ToInt32(lotPrice);
                    int resell = Convert.ToInt32(GetSweetPrice(resellPrice));

                    switch (resellType)
                    {
                        case 1: sellPrice += resell;
                            break;
                        case 2: sellPrice = resell;
                            break;
                    }

                    //You get the point!
                    ParseInventory(SendGet(string.Format(_jsonInv, myUserId, appType.App + "/" + appType.Context), cookieCont));

                    var req = string.Format(sellReq, GetSessId(cookieCont), appType.App, appType.Context, inventList.Find(p => p.Name == markName).AssetId, sellPrice.ToString());

                    SendPost(req, _sellitem, _market, false);
                    doMessage(flag.Resold, 0, markName, true);
                }
                catch (Exception exc)
                {
                    Main.AddtoLog("Resell error: "+ exc.Message);
                    //To Error
                    doMessage(flag.ResellErr, 0, markName, true);
                }

            };
            Thread pTh = new Thread(threadStart);
            pTh.IsBackground = true;
            pTh.Start();

        }




    }

    }


