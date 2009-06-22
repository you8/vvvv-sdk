using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.Diagnostics;
using System.Text.RegularExpressions;


namespace VVVV.Webinterface.HttpServer
{
    class Request
    {


        private string mRequestType;
        private string mHttpVersion;
        private string mFilename;
        private string mFileLocation;
        private string mReqeustedFileExtension;
        private string mRequestedFiletype;
        private SortedDictionary<string, string> mRequestHeadParameterList = new SortedDictionary<string, string>();
        private SortedDictionary<string, string> mRequestBodyParameterList = new SortedDictionary<string, string>();
        private string mMessageHead = "";
        private string mMessageBody = "";
        private Dictionary<string,string> mArguments = new Dictionary<string,string>();




        # region public properties

        public string RequestType
        {
            get
            {
                return mRequestType;
            }
        }

        public string HttpVersion
        {
            get
            {
                return mHttpVersion;
            }
        }

        public string FileName
        {
            get
            {
                return mFilename;
            }
        }

        public string FileLocation
        {
            get
            {
                return mFileLocation;
            }
        }

        public SortedDictionary<string,string> ParameterList
        {
            get 
            {
                return mRequestHeadParameterList;
            }

        }

        public string MessageHead
        {
            get
            {
                return mMessageHead;
            }
        }

        public string MessageBody
        {
            get
            {
                return mMessageBody;
            }
        }

        # endregion public properties








        # region constructor

        public Request(string pRequest, List<string> pFolderToServ)
        {
            mMessageHead = pRequest.Substring(0,pRequest.LastIndexOf(Environment.NewLine));
            mMessageBody = pRequest.Substring(pRequest.LastIndexOf(Environment.NewLine));
            string[] tHeadLines = mMessageHead.Split('\n');
            SplitHeadParameter(tHeadLines);
            SplitFirstLine(tHeadLines[0]);
            
        }

        #endregion constructor




        #region Analyse Http Head


        private void SplitHeadParameter(string[] pParameter)
        {
            int tLength = pParameter.Length;
            for (int i = 1; i < pParameter.Length; i++)
            {
                string line = pParameter[i];
                if (line.Contains(":"))
                {
                    mRequestHeadParameterList.Add(line.Substring(0, line.IndexOf(":")), line.Substring(line.LastIndexOf(":") + 1));
                    Debug.WriteLine(line.Substring(0, line.IndexOf(":")) + ":" + line.Substring(line.LastIndexOf(":") + 1));
                }
            }
        }

        private void SplitFirstLine(string pFirstLine)
        {
            // RequestType
            string[] words = pFirstLine.Split(' ');


            mRequestType = words[0];
            Debug.WriteLine("RequestType: " + mRequestType);

            mHttpVersion = words[2].Substring(words[2].LastIndexOf('/') + 1);
            Debug.WriteLine("HttpVersion: " + mHttpVersion);

            if (words[1] == "/" && words[0] == "GET")
            {
                mFileLocation = words[1];
                mFilename = "index.html";
            }
            else
            {
                //FileName & Location
                string p01 = @"[?][\w\W]*$";
                mFileLocation = Regex.Replace(words[1], p01, "");
                mFilename = mFileLocation.Substring(mFileLocation.LastIndexOf('/') + 1);
            }

            Debug.WriteLine("mFilename: " +  mFilename);
            Debug.WriteLine("mFileLocation: " + mFileLocation);

            //GetProperties
            if(mRequestType =="GET" && mRequestType == "OPTIONS")
            {
                if (words[1].Contains("?"))
                {
                    string p02 = @"[\w\W]+[?]";
                    string getProperties = Regex.Replace(words[1], p02, "");

                    string[] ParamterPairs = getProperties.Split('&');

                    foreach (string pPair in ParamterPairs)
                    {
                        string[] pGetPostParameters = pPair.Split('=');
                        mRequestBodyParameterList.Add(pGetPostParameters[0], pGetPostParameters[1]);
                    }
                }
                else
                {
                    return;
                }
            }
            else if (mRequestType == "POST")
            {
                string tContentTypeHeader;
                mRequestHeadParameterList.TryGetValue("Content-Type", out tContentTypeHeader);

                string[] tValues = tContentTypeHeader.Split(';');

                string ContentType = tValues[0];
                ContentType = ContentType.Trim();

                string tReqeustedFileExtension = String.Empty;

                if (tValues.Length > 1 )
                {
                    string tEncoding = tValues[1];
                }

                //XmlHttpRequest
                if (ContentType == "application/x-www-form-urlencoded")
                {

                }
                //Any Files are send
                else
                {
                    tReqeustedFileExtension = DetectedFileExtension(ContentType);
                    if( tReqeustedFileExtension == "unkown" && ContentType.Contains("javascript"))
                    {
                        tReqeustedFileExtension = ".js";
                    }
                }
            }
            else
            {
                Debug.WriteLine("Unknown Requesttype");
            }
         }


        private string DetectedFileExtension(string pContenType)
        {
            string tReqeustedFileExtension = "unknown";

            Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey("MIME\\Database\\Content Type");

            foreach (string keyName in key.GetSubKeyNames())
            {
                Microsoft.Win32.RegistryKey temp = key.OpenSubKey(keyName);

                if (pContenType.Equals(keyName))
                {
                    if (temp.GetValue("Extension") != null)
                    {
                       tReqeustedFileExtension = temp.GetValue("Extension").ToString();
                    }
                }
            }

            return tReqeustedFileExtension;
        }




        

        #endregion Analyse Http Head







    }
}