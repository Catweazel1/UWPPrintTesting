using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Printing;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Printing;

namespace UWPPrintTesting.classes
{
    class PrintHelper
    {
        /// <summary>
        /// The percent of app's margin width, content is set at 85% (0.85) of the area's width
        /// </summary>
        protected double ApplicationContentMarginLeft = 0.075;

        /// <summary>
        /// The percent of app's margin height, content is set at 94% (0.94) of the area's height
        /// </summary>
        protected double ApplicationContentMargintop = 0.03;

        /// <summary>
        /// PrintDocument is used to prepare the pages for printing
        /// Prepare the pages to print in handlers for the Paginate, GetPreviewPage, and AddPages events
        /// </summary>
        protected PrintDocument printDocument;


        /// <summary>
        /// Marker interface for the document source
        /// </summary>
        protected IPrintDocumentSource printDocumentSource;

        /// <summary>
        /// A list of UIElements used to store the print preview pages.  This gives easy access
        /// to any desired preview page.
        /// </summary>
        internal List<UIElement> printPreviewPages;

        // Event callback which is called after print preview pages are generated.  Photos scenario uses this to do filtering of preview pages
        protected event EventHandler PreviewPagesCreated;

        /// <summary>
        /// First page in the printing-content series
        /// From this "virtual sized" paged content is split(text is flowing) to "printing pages"
        /// </summary>
        protected FrameworkElement firstPage;

        /// <summary>
        /// A reference back to the scenario page used to access XAML elements on the scenario page
        /// </summary>
        protected Page scenarioPage;

        /// <summary>
        /// A hidden canvas used to hold pages we wish to print
        /// </summary>
        protected Canvas PrintCanvas
        {
            get
            {
                return scenarioPage.FindName("PrintCanvas") as Canvas;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="scenarioPage">The scenario page constructing us</param>
        public PrintHelper(Page scenarioPage)
        {
            this.scenarioPage = scenarioPage;
            printPreviewPages = new List<UIElement>();
        }

        /// <summary>
        /// This function registers the app for printing with Windows and sets up the necessary event handlers for the print process.
        /// </summary>
        public virtual void RegisterForPrinting()
        {
            printDocument = new PrintDocument();
            printDocumentSource = printDocument.DocumentSource;
            printDocument.Paginate += CreatePrintPreviewPages;
            printDocument.GetPreviewPage += GetPrintPreviewPage;
            printDocument.AddPages += AddPrintPages;

            PrintManager printMan = PrintManager.GetForCurrentView();
            printMan.PrintTaskRequested += PrintTaskRequested;
        }

        /// <summary>
        /// This funciton unregisters the app for printing with Windows.
        /// </summary>
        public virtual void UnregisterForPrinting()
        {
            if (printDocument == null)
            {
                return;
            }

            printDocument.Paginate -= CreatePrintPreviewPages;
            printDocument.GetPreviewPage -= GetPrintPreviewPage;
            printDocument.AddPages -= AddPrintPages;

            // Remove the handler for printing initialization.
            PrintManager printMan = PrintManager.GetForCurrentView();
            printMan.PrintTaskRequested -= PrintTaskRequested;

            PrintCanvas.Children.Clear();
        }

        public async Task ShowPrintUIAsync()
        {
            // Catch and print out any errors reported
            try
            {
                await PrintManager.ShowPrintUIAsync();
            }
            catch (Exception e)
            {
                MainPage.Current.NotifyUser("Error printing: " + e.Message + ", hr=" + e.HResult, NotifyType.ErrorMessage);
            }
        }

        /// <summary>
        /// This is the event handler for PrintManager.PrintTaskRequested
        /// </summary>
        /// <param name="sender">PrintManager</param>
        /// <param name="e">PrintTaskRequestedEventArgs</param>
        protected virtual void PrintTaskRequested(PrintManager sender, PrintTaskRequestedEventArgs e)
        {
            PrintTask printTask = null;
            printTask = e.Request.CreatePrintTask("C# Printing SDK Sample", sourceRequested =>
            {
                // Print Task event handler is invoked when the print job is completed.
                printTask.Completed += async (s, args) =>
                {
                    // Notify the user when the print operation fails.
                    if (args.Completion == PrintTaskCompletion.Failed)
                    {
                        await scenarioPage.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            MainPage.Current.NotifyUser("Failed to print.", NotifyType.ErrorMessage);
                        });
                    }
                };

                sourceRequested.SetSource(printDocumentSource);
            });
        }

        /// <summary>
        /// this is the event handler for PrintDocument.Paginate.  It creates print preview pages for the app.
        /// </summary>
        /// <param name="sender">PrintDocument</param>
        /// <param name="e">Paginate Event Arguments</param>
        protected virtual void CreatePrintPreviewPages(object sender, PaginateEventArgs e)
        {
            lock (printPreviewPages)
            {
                // clear the cache of preview pages
                printPreviewPages.Clear();

                //Clear the print canvas of preview pages
                PrintCanvas.Children.Clear();

                // This variable keeps track of the last RichTextBlockOverflow element that was added to a page which will be printed.
                RichTextBlockOverflow lastRTBOOnPage;

                // Get the PrintTaskOptions
                PrintTaskOptions printingOptions = ((PrintTaskOptions)e.PrintTaskOptions);

                // Get the page description to determine how big the page is
                PrintPageDescription pageDescription = printingOptions.GetPageDescription(0);

                // We know there is at least one page to be printed.  Passing null as the first parameter to
                // AddOnePrintPreviewPage tells the function to add the first page.
                lastRTBOOnPage = AddOnePrintPreviewPage(null, pageDescription);

                // We know there are more pages to be aded as long as the last RichTextBoxOverflow added to a print preview
                // page has extra content
                while (lastRTBOOnPage.HasOverflowContent && lastRTBOOnPage.Visibility == Windows.UI.Xaml.Visibility.Visible)
                {
                    lastRTBOOnPage = AddOnePrintPreviewPage(lastRTBOOnPage, pageDescription);
                }

                if (PreviewPagesCreated != null)
                {
                    PreviewPagesCreated.Invoke(printPreviewPages, null);
                }

                PrintDocument printDoc = (PrintDocument)sender;

                // Report the number of preview pages created
                printDoc.SetPreviewPageCount(printPreviewPages.Count, PreviewPageCountType.Intermediate);
            }
        }

        /// <summary>
        /// This is the event handler for PrintDocument.GetPrintPreviewPage.  It provides a specific print preview page,
        /// in the form of an UIElement, to an instance of PrintDocument.  PrintDocument subsequently converts the UIElement
        /// into a page that the Windows print system can deal with.
        /// </summary>
        /// <param name="sender">PrintDocument</param>
        /// <param name="e">Arguments containting the preview requested page</param>
		protected virtual void GetPrintPreviewPage(object sender, GetPreviewPageEventArgs e)
        {
            PrintDocument printDoc = (PrintDocument)sender;
            printDoc.SetPreviewPage(e.PageNumber, printPreviewPages[e.PageNumber - 1]);
        }

        /// <summary>
        /// This is the event handler for PrintDocument.AddPages.  It provides all pages to be printed, in the form of
        /// UIElements, to an instance of PrintDocument.  PrintDocument subsequently converts the UIElements
        /// into pages that the Windows print system can deal with.
        /// </summary>
        /// <param name="sender">PrintDocument</param>
        /// <param name="e">Add page event arguments containing a print task options reference.</param>
        protected virtual void AddPrintPages(object sender, AddPagesEventArgs e)
        {
            // Loop over all of the preview pages and add each one to add each page to be printed.
            for (int i = 0; i < printPreviewPages.Count; i++)
            {
                // We should have all pages ready at this point ...
                printDocument.AddPage(printPreviewPages[i]);
            }

            PrintDocument printDoc = (PrintDocument)sender;

            // Indicate that all of the print pages have been provided
            printDoc.AddPagesComplete();
        }

    }
}
