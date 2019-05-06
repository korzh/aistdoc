using System;
using System.Web;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using IdentityModel.Client;
using Newtonsoft.Json;

using Aistant.KbService.Models;

namespace Aistant.KbService {

    class KbErrorMessage {
        public string Message { get; set; }
    }

    public class AistantSettings {

#region API_SETTINGS

#if RELEASE
        [JsonIgnore]
#endif
        public string AuthHost { get; set; } = "https://auth.aistant.com";

#if RELEASE
        [JsonIgnore]
#endif
        public string ApiHost { get; set; } = "https://api.aistant.com";

#if RELEASE
        [JsonIgnore]
#endif
        public string TokenEndpoint { get; set; } = "connect/token";

#if RELEASE
        [JsonIgnore]
#endif
        public string ArticlesEndpoint { get; set; } = "1.0/articles";

#if RELEASE
        [JsonIgnore]
#endif
        public string KbHost { get; set; } = "1.0/knowledge-base";

#if RELEASE
        [JsonIgnore]
#endif
        public string DocsEndpoint { get; set; } = "1.0/docs";

#if RELEASE
        [JsonIgnore]
#endif
        public string PublicEndpoint { get; set; } = "1.0/public";

#if RELEASE
        [JsonIgnore]
#endif
        public string ClientId { get; set; } = "aistant-client";

#if RELEASE
        [JsonIgnore]
#endif
        public string Scope { get; set; } = "openid offline_access profile kb";

        #endregion

        public string Kb { get; set; } = "";

        public Section Section { get; set; }

        public string Username { get; set; } = "";

        public string Password { get; set; } = "";

        public string Team { get; set; } = "";

        public bool AddVersion { get; set; } = true;

        public bool Publish { get; set; } = true;
    }

    public class Section {
        public string Uri { get; set; } = "";

        public string Title { get; set; } = "";
    }

    public class AistantKbService: IDisposable {

        private ILogger _logger;

        private readonly AistantSettings _settings;

        private string _accessToken;

        private AistantKb _currentKb;

        private AistantArticle _mainSection;

        private List<string> _loadedSections = new List<string>();

        private string _nullSectionUri = "NULL";

        private Dictionary<string, Dictionary<string, int>> _indexNumMetaInfo;

        private HttpClient _httpClient;

        public AistantKbService(AistantSettings settings, ILogger logger = null) {
            _logger = logger;

            _settings = settings;

            _currentKb = GetKnowledgeBaseAsync(_settings.Kb).Result;

             if (_currentKb == null) {
                throw new Exception("Knowledge base doesn't exist: " + _settings.Kb);
            }

            _indexNumMetaInfo = new Dictionary<string, Dictionary<string, int>> {
                [_nullSectionUri] = new Dictionary<string, int>()
            };  

            var docs = GetDocsFromKbAsync().Result;

            AnalizeDocuments(docs.Items);

            if (!string.IsNullOrEmpty(_settings.Section.Uri)) {

                _mainSection = GetSectionAsync(_settings.Section.Uri).Result;

                //Create section, if it doen't exist
                if (_mainSection == null) { 
                    _mainSection = new AistantArticle();
                    _mainSection.KbId = _currentKb.Id;
                    _mainSection.Title = _settings.Section.Title;
                    _mainSection.Uri = _settings.Section.Uri;
                    _mainSection.IndexNum = GetMaxIndexNumForDoc(_nullSectionUri) + 1024;
                    _mainSection.Kind = DocItemKind.Section;

                    _mainSection = CreateSectionAsync(_mainSection).Result;

                    _indexNumMetaInfo[_nullSectionUri].Add(_mainSection.Uri, _mainSection.IndexNum);
                    _indexNumMetaInfo[_mainSection.Uri] = new Dictionary<string, int>();
                }
                else {
                    _mainSection = GetSectionByIdAsync(_mainSection.Id).Result;

                    if (_mainSection.Title != _settings.Section.Title
                       || _mainSection.IndexTitle != _settings.Section.Title)
                    {
                        _mainSection.Title = _settings.Section.Title;
                        _mainSection.IndexTitle = _settings.Section.Title;

                        _mainSection = (_settings.AddVersion)
                                       ? CreateVersionAsync(_mainSection).Result
                                       : UpdateLastVersionAsync(_mainSection).Result;
                    }
                    else {
                        Info("Section '" + _mainSection.Title + "'EXISTS with such content in Aistant");
                    }                   
                }

                if (_settings.Publish) {
                    _mainSection = PublishArticleAsync(_mainSection).Result;
                }
            }

            _httpClient = new HttpClient();
        }

        public void SetLogger(ILogger logger) {
            _logger = logger;
        }

        private async Task Login() {
            var url = _settings.AuthHost.CombineWithUri(_settings.TokenEndpoint);
            var client = new TokenClient(url, _settings.ClientId, AuthenticationStyle.PostValues);

            Info($"Connecting to Aistant ({_settings.AuthHost})...");
            //get token
            var tokenResponse = await client.RequestResourceOwnerPasswordAsync(_settings.Username, _settings.Password, _settings.Scope);
            if (tokenResponse.IsError) {
                throw new KbRequestError("Can't login to Aistant.\n" + tokenResponse.Error + ":\n" + tokenResponse.ErrorDescription);
            }

            _accessToken = tokenResponse.AccessToken;
            Info(string.Format("Connected to Aistant"));
        }

 
        /// <summary>
        /// Uploads article to Aistant
        /// </summary>
        /// <param name="uri">uri of article</param>
        /// <param name="title">title of article</param>
        /// <param name="body">body of article</param>
        /// <returns></returns>
        public async Task<bool> UploadArticleAsync(string uri, string title, string body, string excerpt, bool isSection = false) {

            if (string.IsNullOrEmpty(_accessToken)) {
                await Login();
            }

            var http = new HttpClient();
            http.SetBearerToken(_accessToken);

            var articleUri = (_mainSection != null) ? _mainSection.Uri.CombineWithUri(uri) : uri;
            AistantArticle article = await GetArticleAsync(articleUri);
            if (article == null) {
                article = new AistantArticle();
                article.Content = body;
                article.IndexTitle = title;
                article.Title = title;
                article.Uri = articleUri;
                article.Excerpt = excerpt;
                article.KbId = _currentKb.Id;
                article.Kind = (isSection) ? DocItemKind.Section : DocItemKind.Article;

                if (_mainSection != null) {
                    article.ParentId = _mainSection.Id;
                    article.IndexNum = GetMaxIndexNumForDoc(_mainSection.Uri) + 1024;
                    _indexNumMetaInfo[_mainSection.Uri].Add(article.Uri, article.IndexNum);
                }
                else {
                    article.IndexNum = GetMaxIndexNumForDoc(_nullSectionUri) + 1024;
                    _indexNumMetaInfo[_nullSectionUri].Add(article.Uri, article.IndexNum);
                }

                if (isSection) {
                    _indexNumMetaInfo[article.Uri] = new Dictionary<string, int>();
                }
               
                article.FormatType = FormatType.Markdown;

                article = (isSection) ? await CreateSectionAsync(article) : await CreateArticleAsync(article);

            }
            else {
                if (article.Content != body) {
                    article.Content = body;

                    article = (_settings.AddVersion)
                        ? await CreateVersionAsync(article)
                        : await UpdateLastVersionAsync(article);
                }
                else {
                    Info("Article '" + article.Title + "'EXISTS with such content in Aistant");
                }
            }

            if (_settings.Publish) {
                article = await PublishArticleAsync(article);
            }

            return article != null;
        }

        /// <summary>
        /// Upload an article to Aistant into a specified section. If this section doesn't exist - it will be created automatically
        /// </summary>
        /// <param name="sectionUri">Section URI</param>
        /// <param name="sectionTitle">Ssection's title</param>
        /// <param name="articleUri">Article's URI</param>
        /// <param name="articleTitle">Article's title</param>
        /// <param name="articleBody">Article's body</param>
        /// <returns></returns>
        public async Task<bool> UploadArticleAsync(string sectionUri, string sectionTitle, string articleUri, string articleTitle, string articleBody, string articleExcerpt, bool isSection = false) {

            if (string.IsNullOrEmpty(sectionUri)) {
                return await UploadArticleAsync(articleUri, articleTitle, articleBody, articleExcerpt, isSection);
            }

            if (string.IsNullOrEmpty(_accessToken)) {
                await Login();
            }

            var http = new HttpClient();
            http.SetBearerToken(_accessToken);

            string currentSectionUri = (_mainSection != null) ? _mainSection.Uri.CombineWithUri(sectionUri) : sectionUri;

            AistantArticle currentSection = await GetSectionAsync(currentSectionUri);
            if (!_loadedSections.Contains(currentSectionUri)) {
                if (currentSection == null) {
                    currentSection = new AistantArticle();
                    currentSection.KbId = _currentKb.Id;
                    currentSection.Kind = DocItemKind.Section;
                    currentSection.Title = sectionTitle;
                    currentSection.Uri = currentSectionUri;

                    if (_mainSection != null) {
                        currentSection.ParentId = _mainSection.Id;
                        currentSection.IndexNum = GetMaxIndexNumForDoc(_mainSection.Uri) + 1024;
                        _indexNumMetaInfo[_mainSection.Uri].Add(currentSection.Uri, currentSection.IndexNum);
                    }
                    else {
                        currentSection.IndexNum = GetMaxIndexNumForDoc(_nullSectionUri) + 1024;
                        _indexNumMetaInfo[_nullSectionUri].Add(currentSection.Uri, currentSection.IndexNum);
                    }
                    _indexNumMetaInfo[currentSection.Uri] = new Dictionary<string, int>();

                    currentSection = await CreateSectionAsync(currentSection);
                }
                else {
                    currentSection = GetSectionByIdAsync(currentSection.Id).Result;

                    if (sectionTitle != null && (currentSection.Title != sectionTitle
                       || currentSection.IndexTitle != sectionTitle)) {
                        currentSection.Title = sectionTitle;
                        currentSection.IndexTitle = sectionTitle;

                        currentSection = (_settings.AddVersion)
                                       ? CreateVersionAsync(currentSection).Result
                                       : UpdateLastVersionAsync(currentSection).Result;
                    }
                    else {
                        Info("Section '" + currentSection.Title + "'EXISTS with such content in Aistant");
                    }

                }

                if (_settings.Publish) {
                    currentSection = PublishArticleAsync(currentSection).Result;
                }

                _loadedSections.Add(currentSectionUri);
            } 

            var newArticleUri = currentSectionUri.CombineWithUri(articleUri);
            AistantArticle article = await GetArticleAsync(newArticleUri);
            if (article == null) {
                article = new AistantArticle();
                article.Content = articleBody;
                article.IndexTitle = articleTitle;
                article.Title = articleTitle;
                article.Uri = newArticleUri;
                article.KbId = _currentKb.Id;
                article.Excerpt = articleExcerpt;
                article.Kind = (isSection) ? DocItemKind.Section : DocItemKind.Article;
                article.ParentId = currentSection.Id;
                article.IndexNum = GetMaxIndexNumForDoc(currentSection.Uri) + 1024;

                _indexNumMetaInfo[currentSection.Uri].Add(article.Uri, article.IndexNum);

                if (isSection) {
                    _indexNumMetaInfo[article.Uri] = new Dictionary<string, int>();
                }


                article.FormatType = FormatType.Markdown;

                article = (isSection) ? await CreateSectionAsync(article) : await CreateArticleAsync(article);

            }
            else {

                if (article.Content != articleBody || article.Excerpt != articleExcerpt) {
                    article.Content = articleBody;
                    article.Excerpt = articleExcerpt;

                    article = (_settings.AddVersion) 
                        ? await CreateVersionAsync(article)
                        : await UpdateLastVersionAsync(article);
                }
                else {
                    Info("Article '" + article.Title + "' already EXISTS with such content in Aistant");
                }
                
            }

            if (_settings.Publish) {
                article = await PublishArticleAsync(article);
            }

            return article != null;

        }

        #region helpers
        private string ReturnResponseError(string responseStr) {
            try {
                return JsonConvert.DeserializeObject<KbErrorMessage>(responseStr).Message;
            }
            catch {
                return "";
            }
        }

        //Can be used for section or article
        private int GetMaxIndexNumForDoc(string sectionId) {
            int maxIndexNumb = 0;

            var docs = _indexNumMetaInfo[sectionId];
            foreach (var doc in docs) {
                if (maxIndexNumb < doc.Value) {
                    maxIndexNumb = doc.Value;
                }
            }

            return maxIndexNumb;
        }

        private void AnalizeDocuments(List<AistantDocument> items) {
            foreach (var item in items) {
                if (item.Kind == DocItemKind.Section) {
                    _indexNumMetaInfo[item.Uri] = new Dictionary<string, int>();

                    _indexNumMetaInfo[_nullSectionUri].Add(item.Uri, item.IndexNum);

                    AnalizeDocuments(item.Items, item);
                }
                else {
                    _indexNumMetaInfo[_nullSectionUri].Add(item.Uri, item.IndexNum);
                }
            }
        }

        private void AnalizeDocuments(List<AistantDocument> items, AistantDocument parent) {
            foreach (var item in items) {
                if (item.Kind == DocItemKind.Section) {
                    _indexNumMetaInfo[item.Uri] = new Dictionary<string, int>();

                    if (parent.Kind != 0) {
                        _indexNumMetaInfo[parent.Uri].Add(item.Uri, item.IndexNum);
                    }
                    else {
                        _indexNumMetaInfo[_nullSectionUri].Add(item.Uri, item.IndexNum);
                    }

                    AnalizeDocuments(item.Items, item);
                }
                else {
                    if (item.Kind == DocItemKind.Article) {
                        if (parent.Kind != 0) {
                            _indexNumMetaInfo[parent.Uri].Add(item.Uri, item.IndexNum);
                        }
                        else {
                            _indexNumMetaInfo[_nullSectionUri].Add(item.Uri, item.IndexNum);
                        }
                    }
                }
            }
        }
#endregion

#region AISTANT_API
        private async Task<AistantKb> GetKnowledgeBaseAsync(string uri) {

            if (string.IsNullOrEmpty(_accessToken)) {
                await Login();
            }

            _httpClient.SetBearerToken(_accessToken);

            var url = _settings.ApiHost
                        .CombineWithUri(_settings.PublicEndpoint)
                        .CombineWithUri(_settings.Team)
                        .CombineWithUri("kbs")
                        .CombineWithUri(uri);

            var response = await _httpClient.GetAsync(url);
            var responseStr = await response.Content.ReadAsStringAsync();

            return !string.IsNullOrEmpty(responseStr)
                ? JsonConvert.DeserializeObject<AistantKb>(responseStr)
                : null;
        }

        private async Task<AistantDocs> GetDocsFromKbAsync() {
            if (string.IsNullOrEmpty(_accessToken)) {
                await Login();
            }

            if (_currentKb == null) {
                _currentKb = await GetKnowledgeBaseAsync(_settings.Kb);
            }

            if (_currentKb == null) {
                throw new Exception("Knowledge base dose not exist: " + _settings.Kb);
            }

            _httpClient.SetBearerToken(_accessToken);

            var url = _settings.ApiHost
                        .CombineWithUri(_settings.DocsEndpoint)
                        .CombineWithUri("index")
                        .CombineWithUri(_currentKb.Moniker)
                        .CombineWithUri("all");

            var response = await _httpClient.GetAsync(url);
            var responseStr = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.OK) {
                return JsonConvert.DeserializeObject<AistantDocs>(responseStr);
            }
            else {
                throw new KbRequestError(ReturnResponseError(responseStr));
            }

      
        }

        private async Task<AistantArticle> GetSectionAsync(string uri) {
            if (string.IsNullOrEmpty(_accessToken)) {
                await Login();
            }

            if (_currentKb == null) {
                _currentKb = await GetKnowledgeBaseAsync(_settings.Kb);
            }

            if (_currentKb == null) {
                throw new Exception("Knowledge base dose not exist: " + _settings.Kb);
            }

            _httpClient.SetBearerToken(_accessToken);

            var url = _settings.ApiHost
                        .CombineWithUri(_settings.ArticlesEndpoint)
                        .CombineWithUri("uri");

            var uriBuilder = new UriBuilder(url);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query["kb"] = _currentKb.Id;
            query["uri"] = uri;
            uriBuilder.Query = query.ToString();
            url = uriBuilder.ToString();

            var response = await _httpClient.GetAsync(url);
            var responseStr = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.OK) {
                return !string.IsNullOrEmpty(responseStr)
                  ? JsonConvert.DeserializeObject<AistantArticle>(responseStr)
                  : null;
            }
            else if (response.StatusCode == HttpStatusCode.NotFound) {
                return null;
            }
            else {
                throw new KbRequestError(ReturnResponseError(responseStr));
            }

        }

        private async Task<AistantArticle> GetSectionByIdAsync(string id) {
            if (string.IsNullOrEmpty(_accessToken)) {
                await Login();
            }

            if (_currentKb == null) {
                _currentKb = await GetKnowledgeBaseAsync(_settings.Kb);
            }

            if (_currentKb == null) {
                throw new Exception("Knowledge base dose not exist: " + _settings.Kb);
            }

            _httpClient.SetBearerToken(_accessToken);

            var url = _settings.ApiHost
                        .CombineWithUri(_settings.ArticlesEndpoint)
                        .CombineWithUri(id);

            var response = await _httpClient.GetAsync(url);
            var responseStr = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.OK) {
                return !string.IsNullOrEmpty(responseStr)
                  ? JsonConvert.DeserializeObject<AistantArticle>(responseStr)
                  : null;
            }
            else if (response.StatusCode == HttpStatusCode.NotFound) {
                return null;
            }
            else {
                throw new KbRequestError(ReturnResponseError(responseStr));
            }
        }

        private async Task<AistantArticle> CreateSectionAsync(AistantArticle section) {

            if (section.Kind == DocItemKind.Article) {
                throw new InvalidActionException("Use method CreateAcritcleAsync to create an article");
            }

            if (string.IsNullOrEmpty(_accessToken)) {
                await Login();
            }

            if (_currentKb == null) {
                _currentKb = await GetKnowledgeBaseAsync(_settings.Kb);
            }

            if (_currentKb == null) {
                throw new Exception("Knowledge base dose not exist: " + _settings.Kb);
            }

            _httpClient.SetBearerToken(_accessToken);

            var url = _settings.ApiHost.CombineWithUri(_settings.ArticlesEndpoint);

            var content = new StringContent(
                    JsonConvert.SerializeObject(section),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

            var response = await _httpClient.PostAsync(url, content);
            var responseStr = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.OK
               || response.StatusCode == HttpStatusCode.Created) {
                section = JsonConvert.DeserializeObject<AistantArticle>(responseStr);
                Info("Section '" + section.Title + "' has been successfully CREATED in Aistant");
                return section;
            }
            else {
                var responseCodeStr = ((int)response.StatusCode).ToString();
                throw new KbRequestError($"Section has not been created in Aistant. Reason: {responseCodeStr} - {responseStr}");
            }
        }

        private async Task<AistantArticle> GetArticleAsync(string uri) {
            if (string.IsNullOrEmpty(_accessToken)) {
                await Login();
            }

            if (_currentKb == null) {
                _currentKb = await GetKnowledgeBaseAsync(_settings.Kb);
            }

            if (_currentKb == null) {
                throw new Exception("Knowledge base dose not exist: " + _settings.Kb);
            }

            _httpClient.SetBearerToken(_accessToken);

            var url = _settings.ApiHost
                        .CombineWithUri(_settings.ArticlesEndpoint)
                        .CombineWithUri("uri");

            var uriBuilder = new UriBuilder(url);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query["kb"] = _currentKb.Id;
            query["uri"] = uri;
            uriBuilder.Query = query.ToString();
            url = uriBuilder.ToString();

            var response = await _httpClient.GetAsync(url);
            var responseStr = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.OK) {
                return !string.IsNullOrEmpty(responseStr)
                  ? JsonConvert.DeserializeObject<AistantArticle>(responseStr)
                  : null;
            }
            else if (response.StatusCode == HttpStatusCode.NotFound) {
                return null;
            }
            else {
                throw new KbRequestError(response.ReasonPhrase);
            }

        }

        private async Task<AistantArticle> CreateArticleAsync(AistantArticle article) {

            if (article.Kind == DocItemKind.Section) {
                throw new InvalidActionException("Use method CreateSectionAsync to create section");
            }

            if (string.IsNullOrEmpty(_accessToken)) {
                await Login();
            }

            if (_currentKb == null) {
                _currentKb = await GetKnowledgeBaseAsync(_settings.Kb);
            }

            if (_currentKb == null) {
                throw new KbRequestError("Knowledge base dose not exist: " + _settings.Kb);
            }

            _httpClient.SetBearerToken(_accessToken);

            var url = _settings.ApiHost.CombineWithUri(_settings.ArticlesEndpoint);

            var content = new StringContent(
                    JsonConvert.SerializeObject(article),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

            var response = await _httpClient.PostAsync(url, content);
            var responseStr = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.OK
                || response.StatusCode == HttpStatusCode.Created) {
                article = JsonConvert.DeserializeObject<AistantArticle>(responseStr);
                Info($"CREATED: {article.Uri} - {article.IndexTitle}");
                return article;
            }
            else {
                var responseCodeStr = ((int)response.StatusCode).ToString();
                throw new KbRequestError($"Version has not been created in Aistant. Reason: {responseCodeStr} - {responseStr}");
            }

        }

        private async Task<AistantArticle> UpdateLastVersionAsync(AistantArticle article) {
            if (string.IsNullOrEmpty(_accessToken)) {
                await Login();
            }

            if (_currentKb == null) {
                _currentKb = await GetKnowledgeBaseAsync(_settings.Kb);
            }

            if (_currentKb == null) {
                throw new KbRequestError("Knowledge base dose not exist: " + _settings.Kb);
            }

            _httpClient.SetBearerToken(_accessToken);

            var url = _settings.ApiHost
                .CombineWithUri(_settings.ArticlesEndpoint)
                .CombineWithUri(article.Id)
                .CombineWithUri("versions");

            var content = new StringContent(
                    JsonConvert.SerializeObject(article),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

            var response = await _httpClient.PostAsync(url, content);
            var responseStr = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.OK) {
                article = JsonConvert.DeserializeObject<AistantArticle>(responseStr);
                Info($"UPDATED: {article.Uri} - {article.IndexTitle} to version {article.LastVersion}");
                return article;
            }
            else {
                var responseCodeStr = ((int)response.StatusCode).ToString();
                throw new KbRequestError($"Article has not been updated in Aistant. Reason: {responseCodeStr} - {responseStr}");
            }
        }

        private async Task<AistantArticle> CreateVersionAsync(AistantArticle article) {
            if (string.IsNullOrEmpty(_accessToken)) {
                await Login();
            }

            if (_currentKb == null) {
                _currentKb = await GetKnowledgeBaseAsync(_settings.Kb);
            }

            if (_currentKb == null) {
                throw new KbRequestError("Knowledge base dose not exist: " + _settings.Kb);
            }

            article.LastVersion++;

            _httpClient.SetBearerToken(_accessToken);

            var url = _settings.ApiHost
                .CombineWithUri(_settings.ArticlesEndpoint)
                .CombineWithUri(article.Id)
                .CombineWithUri("versions");

            var content = new StringContent(
                    JsonConvert.SerializeObject(article),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

            var response = await _httpClient.PostAsync(url, content);
            var responseStr = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.OK) { }

            if (!string.IsNullOrEmpty(responseStr)) {
                article = JsonConvert.DeserializeObject<AistantArticle>(responseStr);
                Info($"VERSION UPDATE: {article.Uri} - {article.IndexTitle} (version: {article.LastVersion}");
                return article;
            }
            else {
                var responseCodeStr = ((int)response.StatusCode).ToString();
                throw new KbRequestError($"Version has not been updated in Aistant. Reason: {responseCodeStr} - {responseStr}");
            }

        }

        private async Task<AistantArticle> PublishArticleAsync(AistantArticle article) {

            if (article.State == ArticleState.Published) {
                Info($"Article is ALREADY PUBLISHED:  {article.Uri} - {article.IndexTitle} (version: {article.PubVersion})");
                return article;
            }

            if (string.IsNullOrEmpty(_accessToken)) {
                await Login();
            }

            if (_currentKb == null) {
                _currentKb = await GetKnowledgeBaseAsync(_settings.Kb);
            }

            if (_currentKb == null) {
                throw new KbRequestError("Knowledge base dose not exist: " + _settings.Kb);
            }

            _httpClient.SetBearerToken(_accessToken);

            var url = _settings.ApiHost
                .CombineWithUri(_settings.ArticlesEndpoint)
                .CombineWithUri(article.Id)
                .CombineWithUri("publish");

            var content = new StringContent(
                    JsonConvert.SerializeObject(article),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

            var response = await _httpClient.PostAsync(url, content);
            var responseStr = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.OK) {
                article = JsonConvert.DeserializeObject<AistantArticle>(responseStr);
                Info($"PUBLISHED:  {article.Uri} - {article.IndexTitle} (version: {article.PubVersion})");
                return article;
            }
            else {
                var responseCodeStr = ((int)response.StatusCode).ToString();
                throw new KbRequestError($"Article has not been published on Aistant. Reason: {responseCodeStr} - {responseStr}");
            }

        }

        #endregion

        #region Logger
        private void Info(string text) {
            if (_logger != null) {
                _logger.LogInformation(string.Format("{0} OK: {1}", DateTime.Now.ToString("HH:mm:ss"), text));
            }
        }

        private void Error(string text) {
            if (_logger != null) {
                _logger.LogError(string.Format("{0} ERROR: {1}", DateTime.Now.ToString("HH:mm:ss"), text));
            }
          
        }


        public void Dispose()
        {
            _httpClient.Dispose();
        }
        #endregion

    }



    public class KbRequestError : Exception {
        public KbRequestError(string message) : base(message) {
        }
    }

    public class InvalidActionException : Exception {
        public InvalidActionException(string message): base(message){
        }
    }

}
