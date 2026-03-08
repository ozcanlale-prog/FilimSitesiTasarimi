using System.Collections.Generic;

namespace FilimSitesi
{
    public class Movie
    {
        public string PanelName { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public List<string> Cast { get; set; }
        public string TrailerUrl { get; set; }
        public double Rating { get; set; }
        // Optional path or URL to poster image (file path or http/https URL)
        public string PosterPath { get; set; }

        // Optional list of similar movie PanelNames or Titles
        public List<string> Similar { get; set; }
    }
}
