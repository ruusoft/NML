using System;
using System.Linq;
using Nml.Improve.Me.Dependencies;
using System.Text;

namespace Nml.Improve.Me
{
	public class PdfApplicationDocumentGenerator : IApplicationDocumentGenerator
	{
		private readonly IDataContext DataContext;
		private IPathProvider _templatePathProvider;
		public IViewGenerator View_Generator;
		internal readonly IConfiguration _configuration;
		private readonly ILogger<PdfApplicationDocumentGenerator> _logger;
		private readonly IPdfGenerator _pdfGenerator;

		public PdfApplicationDocumentGenerator(
			IDataContext dataContext,
			IPathProvider templatePathProvider,
			IViewGenerator viewGenerator,
			IConfiguration configuration,
			IPdfGenerator pdfGenerator,
			ILogger<PdfApplicationDocumentGenerator> logger)
		{
			//Constrctor for PdfApplicationDocumentGenerator
			try
			{
				DataContext = dataContext is not null ? throw new ArgumentNullException(nameof(dataContext)) : dataContext;
				_templatePathProvider = templatePathProvider ?? throw new ArgumentNullException("templatePathProvider");
				View_Generator = viewGenerator;
				_configuration = configuration;
				_logger = logger ?? throw new ArgumentNullException(nameof(logger));
				_pdfGenerator = pdfGenerator;
			}
			catch(ArgumentNullException ex)
            {
				_logger.LogWarning(ex.Message);
			}
		}
		private string ModelValueCreator(ApplicationViewModel _avm, Application _app, string baseUri)
        {
            try
            {
				baseUri = baseUri.EndsWith("/") ? baseUri[^1..] : baseUri;
				string path = "", view = "";
				
				#region common_properties
				_avm.ReferenceNumber = _app.ReferenceNumber;
				_avm.State = _app.State.ToDescription();
				_avm.FullName = String.Format("{0} {1}", _app.Person.FirstName, _app.Person.Surname);
				_avm.AppliedOn = _app.Date;
				_avm.SupportEmail = _configuration.SupportEmail;
				_avm.Signature = _configuration.Signature;
				#endregion

				if (_app.State == ApplicationState.Pending)
                {
					path = _templatePathProvider.Get("PendingApplication");
					view = View_Generator.GenerateFromPath(string.Format("{0}{1}", baseUri, path), _avm);
				}
				else if(_app.State == ApplicationState.Activated)
                {
					path = _templatePathProvider.Get("ActivatedApplication");
					_avm.LegalEntity = _app.IsLegalEntity ? _app.LegalEntity : null;
					_avm.PortfolioFunds = _app.Products.SelectMany(p => p.Funds);
					_avm.PortfolioTotalAmount = _app.Products.SelectMany(p => p.Funds).Select(f => (f.Amount - f.Fees) * _configuration.TaxRate).Sum();
					view = View_Generator.GenerateFromPath(string.Format("{0}{1}", baseUri, path), _avm);
				}
				else if(_app.State == ApplicationState.InReview)
                {
					path = _templatePathProvider.Get("InReviewApplication");
					StringBuilder inReviewMessage = new("Your application has been placed in review");
					switch (_app.CurrentReview.Reason)
					{
						case "address":
							inReviewMessage.Append(" pending outstanding address verification for FICA purposes.");
							break;
						case "bank":
							inReviewMessage.Append(" pending outstanding bank account verification.");
							break;
						default:
							inReviewMessage.Append(" because of suspicious account behaviour. Please contact support ASAP.");
							break;
					}
					_avm.LegalEntity = _app.IsLegalEntity ? _app.LegalEntity : null;
					_avm.PortfolioFunds = _app.Products.SelectMany(p => p.Funds);
					_avm.PortfolioTotalAmount = _app.Products.SelectMany(p => p.Funds).Select(f => (f.Amount - f.Fees) * _configuration.TaxRate).Sum();
					InReviewApplicationViewModel vm = new()
					{
						InReviewMessage = inReviewMessage.ToString(),
						InReviewInformation = _app.CurrentReview
					};
					vm.Equals(_avm);
					view = View_Generator.GenerateFromPath(string.Format("{0}{1}", baseUri, path), vm);
				}
				else
				{
					_logger.LogWarning(String.Format("The application is in state {0} and no valid document can be generated for it.", _app.State));
				}
				return view;
			}
			catch(Exception ex)
            {
				_logger.LogWarning(ex.Message);
				return "";
            }
        }
		public byte[] Generate(Guid applicationId, string baseUri)
		{
			//PDF generate function
			Application application = DataContext.Applications.Single(app => app.Id == applicationId);
			try 
			{
				if (application is not null)
				{
					string view = "";
					ApplicationViewModel _appViewModel = new ApplicationViewModel();
					view = ModelValueCreator(_appViewModel, application, baseUri);
					PdfOptions pdfOption = new ()
					{
						PageNumbers = PageNumbers.Numeric,
						HeaderOptions = new HeaderOptions
						{
							HeaderRepeat = HeaderRepeat.FirstPageOnly,
							HeaderHtml = PdfConstants.Header
						}
					};
					PdfDocument pdf = _pdfGenerator.GenerateFromHtml(view, pdfOption);
					return pdf.ToBytes();
				}
				else
				{
					_logger.LogWarning(String.Format("No application found for id {0}",applicationId));
					return null;
				}
			}
			catch(Exception ex)
            {
				_logger.LogWarning(ex.Message);
				return null;
			}	
		}
	}
}
