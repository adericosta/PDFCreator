﻿using pdfforge.PDFCreator.UI.Presentation.DesignTime.Helper;
using pdfforge.PDFCreator.UI.Presentation.UserControls.Accounts.AccountViews;

namespace pdfforge.PDFCreator.UI.Presentation.DesignTime
{
    public class DesignTimeDropboxAccountViewModel : DropboxAccountViewModel
    {
        public DesignTimeDropboxAccountViewModel() : base(null, null, new DesignTimeTranslationUpdater())
        {
        }
    }
}
