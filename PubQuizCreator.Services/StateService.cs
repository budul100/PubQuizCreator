namespace PubQuizCreator.Services
{
    public class StateService(IHttpClientFactory httpClientFactory)
    {
        #region Private Fields

        private readonly HttpClient httpClient = httpClientFactory.CreateClient("OllamaHealth");

        #endregion Private Fields

        #region Public Events

        public event Action? OnChange;

        #endregion Public Events

        #region Public Properties

        public bool OllamaOnline { get; private set; }

        public string PageTitle { get; private set; } = "";

        #endregion Public Properties

        #region Public Methods

        public async Task CheckOllamaAsync(CancellationToken ct = default)
        {
            try
            {
                var response = await httpClient.GetAsync("/api/tags", ct);
                OllamaOnline = response.IsSuccessStatusCode;
            }
            catch
            {
                OllamaOnline = false;
            }

            OnChange?.Invoke();
        }

        public void SetPageTitle(string title)
        {
            PageTitle = title;
            OnChange?.Invoke();
        }

        #endregion Public Methods
    }
}