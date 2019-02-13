using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace PrintSample
{
    public sealed partial class PageToPrint : Page
    {
        /// <summary>
        /// Page content to send to the printer
        /// </summary>
        public RichTextBlock TextContentBlock { get; set; }

        public PageToPrint()
        {
            this.InitializeComponent();
            TextContentBlock = TextContent;
        }
    }
}
