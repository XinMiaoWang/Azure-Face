using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Microsoft.VisualBasic;


using System.Net.Http;
using System.Net.Http.Headers;
using System.Web;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Threading;

namespace AzureFace
{
    public partial class Form1 : Form
    {
        static HttpClient client = new HttpClient();
        static string groupId = "Your Group ID";
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }


        // show picture
        private void button1_Click(object sender, EventArgs e)
        {
            clearBox();

            string str = Interaction.InputBox("Please input picture url", "url", "", -1, -1);
            textBox1.Text = str;

            if (str != "")
            {
                Image myPic = Image.FromStream(System.Net.WebRequest.Create(str).GetResponse().GetResponseStream());
                pictureBox1.Image = ResizeImage(myPic, 429,518);
            }

        }

        public void clearBox()
        {
            textBox2.Text = "";
            textBox3.Text = "";
            textBox4.Text = "";
            textBox5.Text = "";
        }

        // Identify
        private async void button2_Click(object sender, EventArgs e)
        {
            clearBox();

            string str = Interaction.InputBox("Please input picture url", "url", "", -1, -1);
            textBox1.Text = str;

            if(str != "")
            {
                Image myPic = Image.FromStream(System.Net.WebRequest.Create(str).GetResponse().GetResponseStream());
                pictureBox1.Image = ResizeImage(myPic, 429, 518);

                string identifyPicURL = str;
                string faceId = await DetectFace(identifyPicURL);
                string personId = await Identify(faceId);

                await GetInfo(personId);
            }
           
        }


        // create person
        private async void button3_Click(object sender, EventArgs e)
        {
            textBox2.Text = "";
            textBox5.Text = "";

            string name = Interaction.InputBox("Please input name", "name", "", -1, -1);
            string gender = Interaction.InputBox("Please input gender", "gender", "", -1, -1);
            string age = Interaction.InputBox("Please input age", "age", "", -1, -1);
            string createPicURL = Interaction.InputBox("Please input picture url", "url", "", -1, -1);

            using (StreamWriter file = new StreamWriter(@".\Training.txt",true))
            {
                file.WriteLine(createPicURL);
            }

            textBox1.Text = createPicURL;
            Image myPic = Image.FromStream(System.Net.WebRequest.Create(createPicURL).GetResponse().GetResponseStream());
            pictureBox1.Image = ResizeImage(myPic, 429, 518);

            string personId = await CreatePerson(name, gender, age);
            string faceId = await DetectFace(createPicURL); // 檢查機制，有人臉才進入下一階段 (??

            await AddFace(personId, createPicURL);
            await Train();

        }

        // show training data
        private void button4_Click(object sender, EventArgs e)
        {
            ImageForm_Load();
        }

        public async Task ImageForm_Load()
        {
            clearBox();

            string[] lines = System.IO.File.ReadAllLines(@".\Training.txt");

            foreach(string line in lines)
            {             
                if (line != "")
                {
                    try
                    {
                        textBox1.Text = line;
                        Image myPic = Image.FromStream(System.Net.WebRequest.Create(line).GetResponse().GetResponseStream());
                        pictureBox1.Image = ResizeImage(myPic, 429, 518);

                        await Task.Delay(2000);
                    }
                    catch (Exception e) { }
                }
            }

            textBox1.Text = "";
            pictureBox1.Image = null;
        }

        public static Bitmap ResizeImage(Image image, int width, int height)
        {
            var srcRect = new Rectangle(0, 0, image.Width, image.Height);
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);
            using (var graphics = Graphics.FromImage(destImage))
                graphics.DrawImage(image, destRect, srcRect, GraphicsUnit.Pixel);

            return destImage;
        }

        public static Bitmap Crop(Image image, Rectangle rect)
        {
            var bitmap = new Bitmap(rect.Width, rect.Height);
            using (Graphics graphics = Graphics.FromImage(bitmap))
                graphics.DrawImage(image, -rect.X, -rect.Y);
            return bitmap;
        }

        public async Task GetInfo(string personId)
        {
            var client = new HttpClient();
            var queryString = HttpUtility.ParseQueryString(string.Empty);

            // Request headers
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "Input Your Key");

            // Request parameters
            var uri = "https://eastus.api.cognitive.microsoft.com/face/v1.0/persongroups/" + groupId + "/persons/" + personId + queryString;

            HttpResponseMessage response;

            // Request body

            string result;

            response = await client.GetAsync(uri);
            result = await response.Content.ReadAsStringAsync();
            //result = result.Replace("[", "").Replace("]", ""); //有[]格式為array，拿掉才能轉json
            //Console.WriteLine(result);

            JObject take = JObject.Parse(result);
            //Console.WriteLine(take["name"].ToString());
            //Console.WriteLine(take["userData"].ToString());
            string name = take["name"].ToString();
            string userData = take["userData"].ToString();
            string gender = userData.Split(',')[0];
            string age = userData.Split(',')[1];

            textBox2.Text = "Name: " + name + "\r\n";
            textBox2.Text = textBox2.Text + "Gender: " + gender + "\r\n";
            textBox2.Text = textBox2.Text + "Age: " + age + "\r\n";
        }


        public async Task<string> DetectFace(string picURL)
        {
            //var client = new HttpClient();
            var queryString = HttpUtility.ParseQueryString(string.Empty);

            // Request headers
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "Input Your Key");

            queryString["returnFaceId"] = "true";
            queryString["returnFaceLandmarks"] = "false";
            queryString["returnFaceAttributes"] = "gender,age,glasses";
            queryString["recognitionModel"] = "recognition_03";
            queryString["returnRecognitionModel"] = "false";
            queryString["detectionModel"] = "detection_01";
            queryString["faceIdTimeToLive"] = "86400";
            // Request parameters
            var uri = "https://eastus.api.cognitive.microsoft.com/face/v1.0/detect?" + queryString;

            HttpResponseMessage response;

            // Request body
            byte[] byteData = Encoding.UTF8.GetBytes("{'url':'" + picURL + "'}");

            string result;
            using (var content = new ByteArrayContent(byteData))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                response = await client.PostAsync(uri, content);
                result = await response.Content.ReadAsStringAsync();
                result = result.Replace("[", "").Replace("]", ""); //最外層有[]，因此回傳的格式為array，拿掉才能轉json
                                                                   //Console.WriteLine(result);


                JObject take = JObject.Parse(result);
                //Console.WriteLine(take.ToString());

                string faceId = take["faceId"].ToString();
                string gender = take["faceAttributes"]["gender"].ToString();
                string age = take["faceAttributes"]["age"].ToString();
                //string glasses = take["faceAttributes"]["glasses"].ToString();

                textBox3.Text = gender;
                textBox4.Text = age;

                return faceId;
            }

        }
        


        public async Task<string> Identify(string faceId)
        {
            var client = new HttpClient();
            var queryString = HttpUtility.ParseQueryString(string.Empty);

            // Request headers
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "Input Your Key");

            // Request parameters
            var uri = "https://eastus.api.cognitive.microsoft.com/face/v1.0/identify?" + queryString;

            HttpResponseMessage response;


            byte[] byteData = Encoding.UTF8.GetBytes("{'faceIds': ['" + faceId + "']," +
                                                      "'personGroupId':'" + groupId + "'," +
                                                      "'maxNumOfCandidatesReturned': 1," +
                                                      "'confidenceThreshold': 0.5" +
                                                     "}");

            string result;
            using (var content = new ByteArrayContent(byteData))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                response = await client.PostAsync(uri, content);
                result = await response.Content.ReadAsStringAsync();
                result = result.Replace("[", "").Replace("]", ""); //有[]格式為array，拿掉才能轉json
                //Console.WriteLine(result);

                JObject take = JObject.Parse(result);
                //Console.WriteLine(take.ToString());

                string personId = take["candidates"]["personId"].ToString();
                string confidence = take["candidates"]["confidence"].ToString();
                //Console.WriteLine(personId);
                textBox5.Text = confidence;

                return personId;
            }
        }

        static async Task AddFace(string personId, string picURL)
        {
            var client = new HttpClient();
            var queryString = HttpUtility.ParseQueryString(string.Empty);

            // Request headers
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "Input Your Key");

            // Request parameters
            queryString["userData"] = "{string}";
            queryString["detectionModel"] = "detection_01";
            var uri = "https://eastus.api.cognitive.microsoft.com/face/v1.0/persongroups/" + groupId + "/persons/" + personId + "/persistedFaces?" + queryString;

            HttpResponseMessage response;

            // Request body
            byte[] byteData = Encoding.UTF8.GetBytes("{'url': '" + picURL + "'}");

            string result;
            using (var content = new ByteArrayContent(byteData))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                response = await client.PostAsync(uri, content);
                result = await response.Content.ReadAsStringAsync();
                result = result.Replace("[", "").Replace("]", ""); //有[]格式為array，拿掉才能轉json
                Console.WriteLine(result);

                //JObject take = JObject.Parse(result);
                //Console.WriteLine(take.ToString());

            }
        }

        static async Task Train()
        {
            var queryString = HttpUtility.ParseQueryString(string.Empty);

            // Request headers
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "Input Your Key");

            var uri = "https://eastus.api.cognitive.microsoft.com/face/v1.0/persongroups/"+ groupId + "/train" + queryString;

            HttpResponseMessage response;

            // Request body
            byte[] byteData = Encoding.UTF8.GetBytes("{body}");

            string result;
            using (var content = new ByteArrayContent(byteData))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                response = await client.PostAsync(uri, content);

                result = await response.Content.ReadAsStringAsync();

                Console.WriteLine("Training Complete !");
            }
        }

        static async Task<string> GetTrainingStatus()
        {
            var queryString = HttpUtility.ParseQueryString(string.Empty);

            // Request headers
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "Input Your Key");

            var uri = "https://eastus.api.cognitive.microsoft.com/face/v1.0/persongroups/" + groupId + "/training?" + queryString;

            HttpResponseMessage response;



            string result;

            response = await client.GetAsync(uri);
            result = await response.Content.ReadAsStringAsync();

            //Console.WriteLine("Training Status : " + result);

            JObject take = JObject.Parse(result);
            string status = take["status"].ToString();

            return status;
        }

        static async Task CreateGroup()
        {
            //var client = new HttpClient();
            var queryString = HttpUtility.ParseQueryString(string.Empty);

            // Request headers
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "Input Your Key");

            string personGroupId = "a";
            var uri = "https://eastus.api.cognitive.microsoft.com/face/v1.0/persongroups/" + personGroupId;

            HttpResponseMessage response;

            // Request body
            byte[] byteData = Encoding.UTF8.GetBytes("{'name':'group2'," +
                                                     "'userData':'homework1214'," +
                                                     "'recognitionModel':'recognition_03'" +
                                                     "}");


            string result;
            using (var content = new ByteArrayContent(byteData))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                response = await client.PutAsync(uri, content);
                result = await response.Content.ReadAsStringAsync();

                Console.WriteLine("PersonGroup Msg:" + result);
            }
        }


        static async Task<string> CreatePerson(string name, string gender, string age)
        {
            //var client = new HttpClient();
            var queryString = HttpUtility.ParseQueryString(string.Empty);

            // Request headers
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "Input Your Key");
            var uri = "https://eastus.api.cognitive.microsoft.com/face/v1.0/persongroups/" + groupId + "/persons?" + queryString;

            HttpResponseMessage response;

            // Request body
            byte[] byteData = Encoding.UTF8.GetBytes("{'name':'" + name + "'," +
                                                     "'userData': '" + gender + "," + age + "'" +
                                                     "}");
            string result;
            using (var content = new ByteArrayContent(byteData))
            {
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                //Console.WriteLine(uri);

                response = await client.PostAsync(uri, content);
                result = await response.Content.ReadAsStringAsync();
                Console.WriteLine(result);

                JObject take = JObject.Parse(result);
                string personId = take["personId"].ToString();
                //Console.WriteLine(personId);

                return personId;
            }
        }


        private void label2_Click(object sender, EventArgs e)
        {

        }

        
    }
}
