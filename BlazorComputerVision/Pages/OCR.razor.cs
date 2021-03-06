using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net;
using System.Net.Http;
using BlazorComputerVision.Models;
using BlazorInputFile;
using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.CognitiveServices.Vision.Face.Models;

namespace BlazorComputerVision.Pages
{
    public class OCRModel : ComponentBase
    {
        protected string DetectedTextLanguage;
        protected string imagePreview;
        protected bool loading = false;
        byte[] imageFileBytes;

        const string DefaultStatus = "Maximum size allowed for the image is 8 MB";
        const bool DefaultDisplayStep2 = true;
        const bool DefaultDisplayStep3 = true;
        protected string status = DefaultStatus;
        protected bool DisplayStep3 = DefaultDisplayStep3;
        protected bool DisplayStep2 = DefaultDisplayStep2;
        protected string age = "";
        protected string gender = "";
        protected string UploadFileName = "";
        protected IList<KinCard> KinCardCollection = new List<KinCard>();

        private IList<DetectedFace> faceList;

        private readonly IFaceClient faceClient = new FaceClient(
            new ApiKeyServiceClientCredentials(subscriptionKey),
            new System.Net.Http.DelegatingHandler[] { });

        const string subscriptionKey = "7f5a52bdb7f4480984585247f973257f";
        const string faceEndpoint = "https://sciencefairface19-20.cognitiveservices.azure.com";

        private Dictionary<string, LanguageDetails> LanguageList = new Dictionary<string, LanguageDetails>();
        const int MaxFileSize = 8 * 2048 * 2048; //8MB

        protected async Task ViewImage(IFileListEntry[] files)
        {
            DisplayStep3 = false;
            var file = files.FirstOrDefault();
            if (file == null)
            {
                return;
            }
            else if (file.Size > MaxFileSize)
            {
                status = $"The file size is {file.Size} bytes, this is more than the allowed limit of {MaxFileSize} bytes.";
                return;
            }
            else if (!file.Type.Contains("image"))
            {
                status = "Please upload a valid image file";
                return;
            }
            else
            {

                var memoryStream = new MemoryStream();
                await file.Data.CopyToAsync(memoryStream);
                imageFileBytes = memoryStream.ToArray();

                status = "Loading...";
                imagePreview = "https://2.bp.blogspot.com/-R4i03mAdarY/WMksQxeJPeI/AAAAAAAAA90/NqrPy8TfTIQNqVI89vJ_uCce45WvklHhwCLcB/s1600/infinity.gif";
                DisplayStep3 = true;

                await File.WriteAllBytesAsync(Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Upload", file.Name), imageFileBytes);
                status = "File Saved on Server";

                EnsureImageRequirements(Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Upload", file.Name));
                status = "Image Requirements Ensured";

                faceList = await FindFaces(file.Name);
                status = "Faces Found:";

                CropToFace(Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Upload", file.Name), faceList.ElementAt(0));

                Image CroppedImage = Image.FromFile(Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Upload", file.Name));
                byte[] bArr = imgToByteArray(CroppedImage);

                string base64String = Convert.ToBase64String(bArr, 0, bArr.Length);

                imagePreview = string.Concat("data:image/png;base64,", base64String);

                age = faceList.ElementAt(0).FaceAttributes.Age.ToString();
                gender = faceList.ElementAt(0).FaceAttributes.Gender.ToString();
                DisplayStep2 = false;

                UploadFileName = file.Name;
            }
        }

        public async Task<IList<DetectedFace>> FindFaces(string imageFilePath)
        {
            // The list of Face attributes to return.
            IList<FaceAttributeType> faceAttributes =
                new FaceAttributeType[]
                {
            FaceAttributeType.Gender, FaceAttributeType.Age,
            FaceAttributeType.Smile, FaceAttributeType.Emotion,
            FaceAttributeType.Glasses, FaceAttributeType.Hair
                };

            faceClient.Endpoint = faceEndpoint;

            // Call the Face API.
            try
            {
                using (Stream imageFileStream = File.OpenRead(Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Upload", imageFilePath)))
                {
                    // The second argument specifies to return the faceId, while
                    // the third argument specifies not to return face landmarks.
                    IList<DetectedFace> faceList =
                        await faceClient.Face.DetectWithStreamAsync(
                            imageFileStream, true, false, faceAttributes);
                    status = $"Detected {faceList.Count} faces.";

                    if (faceList.Count == 1)
                    {
                        return faceList;
                    }
                    else if (faceList.Count > 1)
                    {
                        status = $"Detected {faceList.Count} faces. One person at a time, please.";
                        return new List<DetectedFace>();
                    }
                    else
                    {
                        status = $"Unable to detect faces in this imiage. Please chose another picture or try again.";
                        return new List<DetectedFace>();
                    }
                }
            }
            // Catch and display Face API errors.
            catch (APIErrorException f)
            {
                status = $"API error: {f.Message}";
                return new List<DetectedFace>();
            }
            // Catch and display all other errors.
            catch (Exception e)
            {
                status = $"All other errors: {e.Message}";
                return new List<DetectedFace>();
            }
        }

        // If image is bigger than 6MB, need to resize. This sub is for that, as well as the following sub "Refactor Image"
        private void EnsureImageRequirements(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    // If images are larger than 6000 kilobytes
                    FileInfo fInfo = new FileInfo(filePath);
                    if (fInfo.Length > 6000000)
                    {
                        Image oldImage = Image.FromFile(filePath);

                        ImageFormat originalFormat = oldImage.RawFormat;

                        // manipulate the image / Resize
                        Image tempImage = RefactorImage(oldImage, 48000); ;

                        // Dispose before deleting the file
                        oldImage.Dispose();

                        // Delete the existing file and copy the image to it
                        File.Delete(filePath);

                        // Ensure encoding quality is set to an acceptable level
                        ImageCodecInfo[] encoders = ImageCodecInfo.GetImageEncoders();

                        // Set encoder to fifty percent compression
                        EncoderParameters eps = new EncoderParameters
                        {
                            Param = { [0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 50L) }
                        };

                        ImageCodecInfo ici = (from codec in encoders where codec.FormatID == originalFormat.Guid select codec).FirstOrDefault();

                        // Save the reformatted image and use original file format (jpeg / png / etc) and encoding
                        tempImage.Save(filePath, ici, eps);

                        // Clean up RAM
                        tempImage.Dispose();
                    }

                }
            }
            catch (Exception ex)
            {
                status = "Could not resize oversized image";
            }

        }

        private static Image RefactorImage(Image imgToResize, int maxPixels)
        {
            int sourceWidth = imgToResize.Width;
            int sourceHeight = imgToResize.Height;

            int destWidth = sourceWidth;
            int destHeight = sourceHeight;

            // Resize if needed
            if (sourceWidth > maxPixels || sourceHeight > maxPixels)
            {
                float thePercent = 0;
                float thePercentW = 0;
                float thePercentH = 0;

                thePercentW = maxPixels / (float)sourceWidth;
                thePercentH = maxPixels / (float)sourceHeight;

                if (thePercentH < thePercentW)
                {
                    thePercent = thePercentH;
                }
                else
                {
                    thePercent = thePercentW;
                }

                destWidth = (int)(sourceWidth * thePercent);
                destHeight = (int)(sourceHeight * thePercent);
            }

            Bitmap tmpImage = new Bitmap(destWidth, destHeight, PixelFormat.Format24bppRgb);

            Graphics g = Graphics.FromImage(tmpImage);
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;

            g.DrawImage(imgToResize, 0, 0, destWidth, destHeight);
            g.Dispose();

            return tmpImage;
        }

        // Crop image to just include the single face
        private void CropToFace(string filePath, DetectedFace Face)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    Image oldImage = Image.FromFile(filePath);
                    ImageFormat originalFormat = oldImage.RawFormat;
                    // Crop the image
                    Image tempImage = CropImage(oldImage, Face); ;

                    // Dispose before deleting the file
                    oldImage.Dispose();

                    // Delete the existing file and copy the image to it
                    File.Delete(filePath);

                    // Ensure encoding quality is set to an acceptable level
                    ImageCodecInfo[] encoders = ImageCodecInfo.GetImageEncoders();

                    // Set encoder to fifty percent compression
                    EncoderParameters eps = new EncoderParameters
                    {
                        Param = { [0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 50L) }
                    };

                    ImageCodecInfo ici = (from codec in encoders where codec.FormatID == originalFormat.Guid select codec).FirstOrDefault();

                    // Save the reformatted image and use original file format (jpeg / png / etc) and encoding
                    tempImage.Save(filePath, ici, eps);

                    // Clean up RAM
                    tempImage.Dispose();
                }
            }
            catch (Exception ex)
            {
                status = "Could not resize oversized image";
            }

        }

        private static Image CropImage(Image imgToResize, DetectedFace Face)
        {
            Bitmap tmpImage = new Bitmap(Face.FaceRectangle.Width, Face.FaceRectangle.Height, PixelFormat.Format24bppRgb);

            Graphics g = Graphics.FromImage(tmpImage);
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;

            int sourceWidth = imgToResize.Width;
            int sourceHeight = imgToResize.Height;

            Rectangle SourceRectangle = new Rectangle(0, 0, sourceWidth, sourceHeight);
            Rectangle OutputRectangle = new Rectangle(Face.FaceRectangle.Left, Face.FaceRectangle.Top, Face.FaceRectangle.Width, Face.FaceRectangle.Height);

            g.DrawImage(imgToResize, 0, 0, OutputRectangle, GraphicsUnit.Pixel);
            g.Dispose();

            return tmpImage;
        }



        public byte[] imgToByteArray(Image img)
        {
            using (MemoryStream mStream = new MemoryStream())
            {
                img.Save(mStream, img.RawFormat);
                return mStream.ToArray();
            }
        }

        public async void FindMatches()
        {
            KinCardCollection = new List<KinCard>();
            var TempSortedList = new SortedList<Single, KinCard>(Comparer<Single>.Create((x, y) => y.CompareTo(x)));

            string[] PossibleMatches = Directory.GetFiles(Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Parents"));

            foreach (string PossibleMatch in PossibleMatches)
            {
                KinCard CurrentKinCard = new KinCard();
                Image PossibleMatchImage = Image.FromFile(Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Parents", Path.GetFileName(PossibleMatch)));
                byte[] bArr = imgToByteArray(PossibleMatchImage);

                string base64String = Convert.ToBase64String(bArr, 0, bArr.Length);

                CurrentKinCard.FileName = string.Concat("data:image/png;base64,", base64String);
                CurrentKinCard.Name = AddSpacesToSentence(Path.GetFileNameWithoutExtension(PossibleMatch));

                string JSONResponse = await GoPost(UploadFileName, PossibleMatch);

                Rootobject MyRootObject = JsonSerializer.Deserialize<Rootobject>(JSONResponse);

                CurrentKinCard.Probability = Convert.ToSingle(Math.Round(MyRootObject.predictions[0].probability, 4)*100);

                TempSortedList.Add(CurrentKinCard.Probability, CurrentKinCard);
            }

            foreach (KinCard MyCurrentKinCard in TempSortedList.Values)
            {
                KinCardCollection.Add(MyCurrentKinCard);
            }

            DisplayStep3 = false;
            StateHasChanged();
        }

        string AddSpacesToSentence(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";
            StringBuilder newText = new StringBuilder(text.Length * 2);
            newText.Append(text[0]);
            for (int i = 1; i < text.Length; i++)
            {
                if (char.IsUpper(text[i]) && text[i - 1] != ' ')
                    newText.Append(' ');
                newText.Append(text[i]);
            }
            return newText.ToString();
        }

        public async Task<string> GoPost(string UploadedFile, string PossibleMatchFile)
        {
            HttpResponseMessage response;
            var TargetIP = Environment.GetEnvironmentVariable("TARGET_IP");
            var TargetPort = Environment.GetEnvironmentVariable("TARGET_PORT");

            if (TargetIP != null)
            {
                TargetIP = "127.0.0.1";
            }

            if (TargetPort != null)
            {
                TargetPort = "5001";
            }

            var TargetURL = "http://" + TargetIP + ":" + TargetPort + "/home";

            using (var httpClient = new HttpClient())
            {
                using (var request = new HttpRequestMessage(new HttpMethod("POST"), TargetURL))
                {
                    var multipartContent = new MultipartFormDataContent();
                    multipartContent.Add(new ByteArrayContent(File.ReadAllBytes(Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Upload", UploadedFile))), "file1", Path.GetFileName(Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Upload", UploadedFile)));
                    multipartContent.Add(new ByteArrayContent(File.ReadAllBytes(PossibleMatchFile)), "file2", Path.GetFileName(PossibleMatchFile));
                    request.Content = multipartContent;

                    response = await httpClient.SendAsync(request);
                }
            }

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }
            else
            {
                throw new WebException($"The remote server returned unexpcted status code: {response.StatusCode} - {response.ReasonPhrase}.");
            }
        }
    }

    public class KinCard
    {
        public string FileName { get; set; }
        public string Name { get; set; }
        public Single Probability { get; set; }
    }

    public class Rootobject
    {
        public Prediction[] predictions { get; set; }
        public bool success { get; set; }
    }

    public class Prediction
    {
        public string label { get; set; }
        public float probability { get; set; }
    }

}
