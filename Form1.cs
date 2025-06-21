using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using System.Reflection;
using AForge.Video.DirectShow;
using AForge.Controls;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Net.Http;
using System.Net;
// 解析json
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using AxWMPLib;

namespace Dashboard
{
    public partial class Form1 : Form
    {
        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]

        private static extern IntPtr CreateRoundRectRgn
         (
               int nLeftRect,
               int nTopRect,
               int nRightRect,
               int nBottomRect,
               int nWidthEllipse,
               int nHeightEllipse

         );

        static Form1 _obj;
        public static Form1 Instance
        {
            get {
                if (_obj == null)
                {
                    _obj = new Form1();
                }
                return _obj;
            }
        }
        public Button BackButton
        {
            get { return button3; }
            set { button3 = value; }
        }

        public Form1()
        {
            InitializeComponent();
            search_device();
            axWindowsMediaPlayer1.Visible = false;
            Region = System.Drawing.Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 25, 25));
        }

        private void button3_Click(object sender, EventArgs e)
        {
            
            videoSourcePlayer1.Stop();
            Application.Exit();
        }


        private void label1_Click(object sender, EventArgs e)
        {

        }

        private FilterInfoCollection vedioDevices = null;
        // 检测设备
        private void button7_Click(object sender, EventArgs e)
        {
            search_device();
        }
        private void search_device()
        {
            vedioDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (vedioDevices != null && vedioDevices.Count > 0)
            {
                comboBox1.Items.Clear();
                foreach (FilterInfo device in vedioDevices)
                {
                    comboBox1.Items.Add(device.Name);
                }
                comboBox1.SelectedIndex = 0;
            }
        }
        // 开启摄像头
        private void button8_Click(object sender, EventArgs e)
        {
            CameraConn();
        }
        private VideoCaptureDevice videoSource = null;
        private void CameraConn()
        {
            if (comboBox1.Items.Count<=0)
            {
                return;
            }
            videoSource = new VideoCaptureDevice(vedioDevices[comboBox1.SelectedIndex].MonikerString);
            videoSource.VideoResolution = videoSource.VideoCapabilities[0];

            videoSource.DesiredFrameRate = 1;

            videoSourcePlayer1.VideoSource = videoSource;
            videoSourcePlayer1.Start();
        }
        // 拍照
        private void button9_Click(object sender, EventArgs e)
        {
            if (!videoSourcePlayer1.IsRunning)
            {
                MessageBox.Show("摄像头未启动");
                return;
            }
            try
            {
                if (videoSourcePlayer1.IsRunning)
                {
                    BitmapSource bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                                    videoSourcePlayer1.GetCurrentVideoFrame().GetHbitmap(),
                                    IntPtr.Zero,
                                     Int32Rect.Empty,
                                    BitmapSizeOptions.FromEmptyOptions());
                    PngBitmapEncoder pE = new PngBitmapEncoder();
                    pE.Frames.Add(BitmapFrame.Create(bitmapSource));
                    string picName = GetImagePath() + "\\" + DateTime.Now.ToFileTime() + ".jpg";
                    // 若存在文件则删除旧文件
                    if (File.Exists(picName))
                    {
                        File.Delete(picName);
                    }
                    // 保存文件
                    using (Stream stream = File.Create(picName))
                    {
                        pE.Save(stream);
                        selectedImage1Path = picName;
                        MessageBox.Show("照片保存成功，保存在：\n"+ picName);
                    }

                    pictureBox2.SizeMode = PictureBoxSizeMode.Zoom;
                    pictureBox2.Load(picName);
                }
            }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        private string GetImagePath()
        {
            string personImgPath = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory)
                         + Path.DirectorySeparatorChar.ToString() + "PersonImg";
            if (!Directory.Exists(personImgPath))
            {
                Directory.CreateDirectory(personImgPath);
            }

            return personImgPath;
        }
        // 测试能否正常获取AccessToken
        private void button1_Click(object sender, EventArgs e)
        {
            string token = getAccessToken();
            if (!string.IsNullOrEmpty(token))
            {
                MessageBox.Show($"获取AccessToken成功，access_Token为：\n{token}");
            }
            return;
        }
        // 转换base64编码
        public static string ConvertJpgToBase64(string imagePath)
        {
            try
            {
                using (FileStream fileStream = new FileStream(imagePath, FileMode.Open, FileAccess.Read))
                {
                    byte[] imageBytes = new byte[fileStream.Length];
                    fileStream.Read(imageBytes, 0, (int)fileStream.Length);
                    return Convert.ToBase64String(imageBytes);
                }
            }
            catch (Exception ex)
            {
                // 处理异常，例如文件不存在或无法读取
                Console.WriteLine($"转换错误: {ex.Message}");
                return null;
            }
        }
        // 人脸识别
        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                if (selectedImage1Path != "")
                {
                    string jpg_base64 = ConvertJpgToBase64(selectedImage1Path);
                    string result = faceDetect(jpg_base64); // 调用识别

                    JObject json = JObject.Parse(result);
                    if (json["error_code"].ToString() == "0")
                    {
                        JObject face = (JObject)json["result"]["face_list"][0];
                        string gender = face["gender"]["type"].ToString();           // male / female
                        string age = face["age"].ToString();
                        string expression = face["expression"]["type"].ToString();   // none / smile / laugh
                        string beauty = face["beauty"].ToString();
                        string emotion = face["emotion"]["type"].ToString();         // angry, happy, sad...

                        string msg = $"检测结果如下：\n\n" +
                                     $"性别：{(gender == "male" ? "男" : "女")}\n" +
                                     $"年龄：{age} 岁\n" +
                                     $"表情：{ExpressionToCN(expression)}\n" +
                                     $"颜值评分：{beauty}\n" +
                                     $"情绪：{EmotionToCN(emotion)}";

                        MessageBox.Show(msg, "人脸检测结果", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("识别失败：" + json["error_msg"]);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("人脸识别错误：" + ex.Message);
            }
        }
        private string ExpressionToCN(string type)
        {
            switch (type)
            {
                case "smile": return "微笑";
                case "laugh": return "大笑";
                case "none": return "无表情";
                default: return type;
            }
        }

        private string EmotionToCN(string type)
        {
            switch (type)
            {
                case "angry": return "愤怒";
                case "disgust": return "厌恶";
                case "fear": return "恐惧";
                case "happy": return "高兴";
                case "sad": return "伤心";
                case "surprise": return "惊讶";
                case "neutral": return "无情绪";
                default: return type;
            }
        }



        // 之所以删除了类，是因为C#中对于静态方法是不能够修改组件属性的
        // 百度提供的相关示例代码
        // 百度云中开通对应服务应用的 API Key 建议开通应用的时候多选服务
        private static String clientId = "pOz3fiB6lt7C8fbdXXgkzSi2";
        private static String clientSecret = "uCIlpphE3YOJNgWvdKx3e7IlTrCcMABB";
        private string getAccessToken()
        {
            string host = "https://aip.baidubce.com/oauth/2.0/token";
            string grant_type = "client_credentials";
            string url = $"{host}?grant_type={grant_type}&client_id={clientId}&client_secret={clientSecret}";
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.ContentType = "application/json;charset=UTF-8";
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    string result = reader.ReadToEnd();
                    JObject json = JObject.Parse(result);
                    return json["access_token"].ToString();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("获取AccessToken失败：" + ex.Message);
                return null;
            }
        }

        // 人脸检测
        private string faceDetect(string jpg_base64)
        {
            string access_token = getAccessToken();
            string url = $"https://aip.baidubce.com/rest/2.0/face/v3/detect?access_token={access_token}";
            var param = new Dictionary<string, object>
            {
                {"image", jpg_base64 },
                {"image_type", "BASE64"},
                {"face_field", "age,beauty,expression,face_shape,gender,glasses,emotion"}
            };

                    return Post(url, param);
        }

        // 人脸对比
        public string faceMatch(string jpg1_base64, string jpg2_base64)
        {
            string access_token = getAccessToken();
            string url = $"https://aip.baidubce.com/rest/2.0/face/v3/match?access_token={access_token}";

            var images = new List<Dictionary<string, object>>
    {
        new Dictionary<string, object> {
            {"image", jpg1_base64},
            {"image_type", "BASE64"},
            {"face_type", "LIVE"},
            {"quality_control", "LOW"},
            {"liveness_control", "NONE"}
        },
        new Dictionary<string, object> {
            {"image", jpg2_base64},
            {"image_type", "BASE64"},
            {"face_type", "LIVE"},
            {"quality_control", "LOW"},
            {"liveness_control", "NONE"}
        }
    };

            string jsonData = JsonConvert.SerializeObject(images);
            return PostRaw(url, jsonData);
        }

        // 人脸搜索
        public string faceSearch(string jpg_base64)
        {
            string access_token = getAccessToken();
            string url = $"https://aip.baidubce.com/rest/2.0/face/v3/search?access_token={access_token}";

            var param = new Dictionary<string, object>
    {
        {"image", jpg_base64 },
        {"image_type", "BASE64"},
        {"group_id_list", "119306708"}, // 请替换成你自己创建的人脸库group_id
        {"quality_control", "LOW"},
        {"liveness_control", "NORMAL"}
    };

            return Post(url, param);
        }

        // 人脸注册
        public string add(string jpg_base64)
        {
            string access_token = getAccessToken();
            string url = $"https://aip.baidubce.com/rest/2.0/face/v3/faceset/user/add?access_token={access_token}";

            var param = new Dictionary<string, object>
    {
        {"image", jpg_base64 },
        {"image_type", "BASE64"},
        {"group_id", "119306708"}, // 替换为你的 group_id
        {"user_id", textBox1.Text }, // 用户ID，支持任意唯一字符串
        {"liveness_control", "NORMAL"},
        {"quality_control", "LOW"}
    };

            return Post(url, param);
        }
        private string Post(string url, Dictionary<string, object> data)
        {
            try
            {
                string jsonData = JsonConvert.SerializeObject(data);
                byte[] postData = Encoding.UTF8.GetBytes(jsonData);

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.ContentLength = postData.Length;

                using (Stream stream = request.GetRequestStream())
                {
                    stream.Write(postData, 0, postData.Length);
                }

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                return $"{{\"error\":\"{ex.Message}\"}}";
            }
        }

        private string PostRaw(string url, string rawJson)
        {
            try
            {
                byte[] postData = Encoding.UTF8.GetBytes(rawJson);

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.ContentLength = postData.Length;

                using (Stream stream = request.GetRequestStream())
                {
                    stream.Write(postData, 0, postData.Length);
                }

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                return $"{{\"error\":\"{ex.Message}\"}}";
            }
        }


        string selectedImage1Path = "";
        string selectedImage2Path = "";
        private void button4_Click(object sender, EventArgs e)
        {
            try
            {
                string jpg1_base64 = "";
                string jpg2_base64 = "";
                if (selectedImage1Path != "")
                {
                    jpg1_base64 = ConvertJpgToBase64(selectedImage1Path);
                }
                if (selectedImage2Path != "")
                {
                    jpg2_base64 = ConvertJpgToBase64(selectedImage2Path);
                }
                string result = faceMatch(jpg1_base64, jpg2_base64);
                JObject jsonObject = JObject.Parse(result);
                double score = (double)jsonObject["result"]["score"];
                if (score > 70)
                {
                    MessageBox.Show("人脸对比成功，是同一个人");
                }
                else{
                    MessageBox.Show("人脸对比成功，不是同一个人");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("人脸识别错误：" + ex.Message);
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            try
            {
                if (selectedImage1Path != "")
                {
                    string jpg_base64 = ConvertJpgToBase64(selectedImage1Path);
                    faceSearch(jpg_base64);
                }
                MessageBox.Show("人脸识别成功");
            }
            catch (Exception ex)
            {
                MessageBox.Show("人脸识别错误：" + ex.Message);
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            try
            {
                if (selectedImage1Path != "")
                {
                    string jpg_base64 = ConvertJpgToBase64(selectedImage1Path);
                    add(jpg_base64);
                }
                MessageBox.Show("人脸注册成功");
            }
            catch (Exception ex)
            {
                MessageBox.Show("人脸注册错误：" + ex.Message);
            }
        }

        private void button10_Click(object sender, EventArgs e)
        {
            try
            {
                if (selectedImage1Path != "")
                {
                    string jpg_base64 = ConvertJpgToBase64(selectedImage1Path);
                    string result = faceSearch(jpg_base64);
                    string UserId = textBox1.Text;
                    var jObject = JObject.Parse(result);
                    JToken codeToken = jObject.SelectToken("error_code");
                    JToken UserIdToken = jObject.SelectToken("result.user_list[0].user_id");
                    int code = int.Parse(codeToken?.ToString());
                    string UserIdFind = UserIdToken?.ToString();
                    if (code != 0 || UserId != UserIdFind)
                    {
                        throw new Exception("登录失败");
                    }
                    play_success_login_music();
                    MessageBox.Show("人脸登录成功");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("人脸识别错误：" + ex.Message);
            }
        }

        private void play_success_login_music()
        {
            try
            {
                axWindowsMediaPlayer1.URL = "SUCCESS.mp3";
                axWindowsMediaPlayer1.Ctlcontrols.play();
            }
            catch (Exception ex)
            {
                MessageBox.Show("音乐播放错误："+ex.Message);
            }
        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void pictureBox2_Click(object sender, EventArgs e)
        {
            try
            {
                openFileDialog1.Filter = "选择图片|*.*";
                if (openFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    selectedImage1Path = openFileDialog1.FileName;
                }
                pictureBox2.SizeMode = PictureBoxSizeMode.Zoom;
                pictureBox2.Load(selectedImage1Path);
            }
            catch (Exception ex)
            {
                MessageBox.Show("错误：" + ex.Message);
            }
        }

        private void pictureBox3_Click(object sender, EventArgs e)
        {
            try
            {
                openFileDialog2.Filter = "选择图片|*.*";
                if (openFileDialog2.ShowDialog() == DialogResult.OK)
                {
                    selectedImage2Path = openFileDialog2.FileName;
                }
                pictureBox3.SizeMode = PictureBoxSizeMode.Zoom;
                pictureBox3.Load(selectedImage2Path);
            }
            catch(Exception ex)
            {
                MessageBox.Show("错误："+ex.Message);
            }
        }

        private void videoSourcePlayer1_Click(object sender, EventArgs e)
        {

        }
        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {
            // 暂不处理内容，你以后可以扩展功能（比如实时统计字数）
        }

    }





}
