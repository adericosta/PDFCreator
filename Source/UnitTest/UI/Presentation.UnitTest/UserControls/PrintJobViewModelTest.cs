﻿using NSubstitute;
using NUnit.Framework;
using pdfforge.PDFCreator.Conversion.Jobs.JobInfo;
using pdfforge.PDFCreator.Conversion.Jobs.Jobs;
using pdfforge.PDFCreator.Conversion.Jobs.Query;
using pdfforge.PDFCreator.Conversion.Settings;
using pdfforge.PDFCreator.Conversion.Settings.Enums;
using pdfforge.PDFCreator.Core.Services;
using pdfforge.PDFCreator.Core.SettingsManagement;
using pdfforge.PDFCreator.Core.Workflow;
using pdfforge.PDFCreator.Core.Workflow.Exceptions;
using pdfforge.PDFCreator.Core.Workflow.Queries;
using pdfforge.PDFCreator.UI.Interactions;
using pdfforge.PDFCreator.UI.Interactions.Enums;
using pdfforge.PDFCreator.UI.Presentation.Helper.Translation;
using pdfforge.PDFCreator.UI.Presentation.UserControls.PrintJob;
using pdfforge.PDFCreator.UI.Presentation.Workflow;
using pdfforge.PDFCreator.UnitTest.UnitTestHelper;
using pdfforge.PDFCreator.Utilities;
using pdfforge.PDFCreator.Utilities.IO;
using pdfforge.PDFCreator.Utilities.Threading;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Data;
using SystemInterface.IO;
using SystemWrapper.IO;
using Translatable;

namespace Presentation.UnitTest.UserControls
{
    [TestFixture]
    public class PrintJobViewModelTest
    {
        private PdfCreatorSettings _settings;
        private ConversionProfile _pdfProfile;
        private ConversionProfile _pngProfile;
        private IJobInfoQueue _jobInfoQueue;
        private IFileNameQuery _saveFileQuery;
        private UnitTestInteractionRequest _interactionRequest;
        private readonly PrintJobViewTranslation _translation = new PrintJobViewTranslation();
        private IFile _file;
        private static string _filepathFromSaveDialog = @"DirectoryFromSaveDialog\FilenameFromSaveDialog.pdf";
        private readonly string _folderFromSaveDialog = Path.GetDirectoryName(_filepathFromSaveDialog);
        private readonly string _filenameFromSaveDialog = Path.GetFileName(_filepathFromSaveDialog);
        private IPathUtil _pathUtil;
        private IDirectoryHelper _directoryHelper;
        private IInteractiveProfileChecker _interactiveProfileChecker;

        [SetUp]
        public void Setup()
        {
            _settings = new PdfCreatorSettings(null);
            _jobInfoQueue = Substitute.For<IJobInfoQueue>();
            _interactionRequest = new UnitTestInteractionRequest();
            _file = Substitute.For<IFile>();
            _directoryHelper = Substitute.For<IDirectoryHelper>();
            _interactiveProfileChecker = Substitute.For<IInteractiveProfileChecker>();

            _pdfProfile = new ConversionProfile
            {
                Name = "PDF Profile",
                OutputFormat = OutputFormat.Pdf,
                FileNameTemplate = "X:\\test.pdf",
                TargetDirectory = "c:\\Test\\"
            };

            _pngProfile = new ConversionProfile
            {
                Name = "PDF Profile",
                OutputFormat = OutputFormat.Png,
                FileNameTemplate = "X:\\test.png",
                TargetDirectory = "c:\\Test\\"
            };

            _settings.ConversionProfiles.Add(_pdfProfile);
            _settings.ConversionProfiles.Add(_pngProfile);
        }

        private PrintJobViewModel BuildViewModel(bool saveDialogResult = true)
        {
            MockSaveFileDialog(saveDialogResult);
            var settingsProvider = Substitute.For<ISettingsProvider>();
            settingsProvider.Settings.Returns(_settings);
            _pathUtil = Substitute.For<IPathUtil>();

            _pathUtil.IsValidRootedPath(Arg.Any<string>()).Returns(true);
            _pathUtil.MAX_PATH.Returns(259);
            _pathUtil.GetLongDirectoryName(Arg.Any<string>()).Returns(x => Path.GetDirectoryName(x.Arg<string>()));

            var pathUtil = new PathUtil(new PathWrap(), new DirectoryWrap()); //todo
            _pathUtil.GetFileName(Arg.Any<string>()).Returns(s => pathUtil.GetFileName(s.Arg<string>()));
            _pathUtil.Combine(Arg.Any<string>(), Arg.Any<string>()).Returns(s => pathUtil.Combine(s.ArgAt<string>(0), s.ArgAt<string>(1)));

            return new PrintJobViewModel(settingsProvider, new TranslationUpdater(new TranslationFactory(), new ThreadManager()),
                _jobInfoQueue, _saveFileQuery, _interactionRequest, new DesignTimeCommandLocator(),
                null, null, null, _pathUtil, _file, null, null, _directoryHelper, _interactiveProfileChecker);
        }

        private Job BuildJob(ConversionProfile profile)
        {
            var job = new Job(new JobInfo(), profile, new JobTranslations(), new Accounts());

            job.OutputFilenameTemplate = profile.FileNameTemplate;

            return job;
        }

        private void MockSaveFileDialog(bool success)
        {
            _saveFileQuery = Substitute.For<IFileNameQuery>();
            var outputFilenameResult = new OutputFilenameResult(_filepathFromSaveDialog, OutputFormat.Pdf);
            var queryResultOutputFilenameResult = new QueryResult<OutputFilenameResult>(success, outputFilenameResult);
            _saveFileQuery.GetFileName(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<OutputFormat>())
                .Returns(queryResultOutputFilenameResult);
        }

        [Test]
        public void SetJob_UpdatesJob()
        {
            var vm = BuildViewModel();
            var job = BuildJob(_pdfProfile);

            vm.ExecuteWorkflowStep(job);

            Assert.AreSame(vm.Job, job);
        }

        [Test]
        public void SetJob_RaisesPropertyChanged()
        {
            var changedProperties = new List<string>();
            var vm = BuildViewModel();
            vm.PropertyChanged += (sender, args) => changedProperties.Add(args.PropertyName);

            var job = BuildJob(_pdfProfile);

            vm.ExecuteWorkflowStep(job);

            var expectedProperties = new[]
            {
                nameof(vm.Job),
                nameof(vm.SelectedProfile),
                nameof(vm.OutputFilename),
                nameof(vm.OutputFolder),
                nameof(vm.OutputFormat),
                nameof(vm.Title),
                nameof(vm.Author),
                nameof(vm.Subject),
                nameof(vm.Keyword)
            };

            CollectionAssert.AreEquivalent(expectedProperties, changedProperties);
        }

        [Test]
        public void SetJob_UpdatesFilenameProperties()
        {
            var vm = BuildViewModel();
            var job = BuildJob(_pdfProfile);

            vm.SetJob(job);

            Assert.AreEqual("X:\\", vm.OutputFolder);
            Assert.AreEqual("test.pdf", vm.OutputFilename);
        }

        [Test]
        public void OutputFormat_WhenSet_UpdatesFilename()
        {
            var vm = BuildViewModel();
            var job = BuildJob(_pdfProfile);
            vm.SetJob(job);

            vm.OutputFormat = OutputFormat.Txt;

            Assert.AreEqual("test.txt", vm.OutputFilename);
        }

        [Test]
        public void SaveCommand_Execute_JobProfileIsValid_DoesNotThrowException()
        {
            var vm = BuildViewModel();
            var job = BuildJob(_pdfProfile);

            vm.ExecuteWorkflowStep(job);

            Assert.DoesNotThrow(() => vm.SaveCommand.Execute(null));
        }

        [Test]
        public void SaveCommand_Execute_JobProfileIsValid_CallsFinishedEvent()
        {
            var eventWasRaised = false;
            var vm = BuildViewModel();
            var job = BuildJob(_pdfProfile);
            _interactiveProfileChecker.CheckWithErrorResultInOverlay(job).Returns(true);

            vm.StepFinished += (sender, args) => eventWasRaised = true;
            vm.ExecuteWorkflowStep(job);

            vm.SaveCommand.Execute(null);

            Assert.IsTrue(eventWasRaised);
        }

        [Test]
        public void SaveCommand_Execute_JobProfileIsValid_ProfileCheckCopiesJobPasswords()
        {
            var expectedPassword = "PDF Owner Password";
            var vm = BuildViewModel();
            var job = BuildJob(_pdfProfile);
            _interactiveProfileChecker.CheckWithErrorResultInOverlay(job).Returns(true);
            job.Profile.PdfSettings.Security.OwnerPassword = expectedPassword;

            vm.ExecuteWorkflowStep(job);
            vm.SaveCommand.Execute(null);

            Assert.AreEqual(expectedPassword, job.Passwords.PdfOwnerPassword);
        }

        [Test]
        public void SaveCommand_Execute_JobProfileIsValid_NoUserNotification()
        {
            var expectedPassword = "PDF Owner Password";
            var vm = BuildViewModel();
            var job = BuildJob(_pdfProfile);
            job.Profile.PdfSettings.Security.OwnerPassword = expectedPassword;
            _interactiveProfileChecker.CheckWithErrorResultInOverlay(job).Returns(true);

            vm.ExecuteWorkflowStep(job);
            vm.SaveCommand.Execute(null);

            _interactionRequest.AssertWasNotRaised<MessageInteraction>();
        }

        [Test]
        public void SaveCommand_Execute_JobProfileIsNotValid_DoesNotCallFinishEvent()
        {
            var vm = BuildViewModel();
            var job = BuildJob(_pdfProfile);
            vm.ExecuteWorkflowStep(job);
            var eventWasRaised = false;
            vm.StepFinished += (sender, args) => eventWasRaised = true;

            vm.SaveCommand.Execute(null);

            Assert.IsFalse(eventWasRaised);
        }

        [Test]
        public void SaveCommand_Execute_ProfileIsValid_FilePathFromSaveDialog_FileDoesNotExist_DoesNotRaiseInteraction_CallsFinishEvent()
        {
            var vm = BuildViewModel();
            var job = BuildJob(_pdfProfile);
            vm.ExecuteWorkflowStep(job);
            _interactiveProfileChecker.CheckWithErrorResultInOverlay(job).Returns(true);
            var stepFinishedRaised = false;
            vm.StepFinished += (sender, args) => stepFinishedRaised = true;

            vm.BrowseFileCommand.Execute(null);
            _file.Exists(Arg.Any<string>()).Returns(false);

            vm.SaveCommand.Execute(null);

            _interactionRequest.AssertWasNotRaised<MessageInteraction>();
            Assert.IsTrue(stepFinishedRaised);
        }

        [Test]
        public void SaveCommand_Execute_ProfileIsValid_FilePathFromSaveDialog_FileExists_DoesNotRaiseInteraction_CallsFinishEvent()
        {
            var vm = BuildViewModel();
            var job = BuildJob(_pdfProfile);
            vm.ExecuteWorkflowStep(job);
            _interactiveProfileChecker.CheckWithErrorResultInOverlay(job).Returns(true);
            var stepFinishedRaised = false;
            vm.StepFinished += (sender, args) => stepFinishedRaised = true;

            vm.BrowseFileCommand.Execute(null);
            _file.Exists(Arg.Any<string>()).Returns(true);

            vm.SaveCommand.Execute(null);

            _interactionRequest.AssertWasNotRaised<MessageInteraction>();
            Assert.IsTrue(stepFinishedRaised);
        }

        [Test]
        public void SaveCommand_Execute_ProfileIsValid_FilePathNotFromSaveDialog_FileDoesNotExist_DoesNotRaiseInteraction_CallsFinishEvent()
        {
            var vm = BuildViewModel();
            var job = BuildJob(_pdfProfile);
            vm.ExecuteWorkflowStep(job);
            _interactiveProfileChecker.CheckWithErrorResultInOverlay(job).Returns(true);
            var stepFinishedRaised = false;
            vm.StepFinished += (sender, args) => stepFinishedRaised = true;

            vm.BrowseFileCommand.Execute(null);
            _file.Exists(Arg.Any<string>()).Returns(false);
            vm.OutputFilename += "not" + _filenameFromSaveDialog;
            vm.OutputFolder += "not" + _folderFromSaveDialog;

            vm.SaveCommand.Execute(null);

            _interactionRequest.AssertWasNotRaised<MessageInteraction>();
            Assert.IsTrue(stepFinishedRaised);
        }

        [Test]
        public void SaveCommand_Execute_ProfileIsValid_FilePathNotFromSaveDialog_FileExist_NotifysUserWithCorrectInteraction()
        {
            var vm = BuildViewModel();
            var job = BuildJob(_pdfProfile);
            vm.ExecuteWorkflowStep(job);
            _interactiveProfileChecker.CheckWithErrorResultInOverlay(job).Returns(true);

            vm.BrowseFileCommand.Execute(null);
            _file.Exists(Arg.Any<string>()).Returns(true);
            vm.OutputFilename = "not" + _filenameFromSaveDialog;
            vm.OutputFolder = "not" + _folderFromSaveDialog;
            var expectedDir = Path.Combine(vm.OutputFolder, vm.OutputFilename);

            vm.SaveCommand.Execute(null);

            var interaction = _interactionRequest.AssertWasRaised<MessageInteraction>();
            Assert.AreEqual(_translation.ConfirmSaveAs.ToUpper(), interaction.Title, "Title");
            Assert.AreEqual(_translation.GetFileAlreadyExists(expectedDir), interaction.Text, "Message");
            Assert.AreEqual(MessageIcon.Exclamation, interaction.Icon, "Icon");
            Assert.AreEqual(MessageOptions.YesNo, interaction.Buttons, "Buttons");
        }

        [Test]
        public void SaveCommand_Execute_ProfileIsValid_UserCanceledSaveFileDilaog_FileExist_NotifysUserWithCorrectInteraction()
        {
            var vm = BuildViewModel(saveDialogResult: false); //User cancels SaveFileDialog
            var job = BuildJob(_pdfProfile);
            vm.ExecuteWorkflowStep(job);
            _interactiveProfileChecker.CheckWithErrorResultInOverlay(job).Returns(true);
            vm.BrowseFileCommand.Execute(null);
            _file.Exists(Arg.Any<string>()).Returns(true);
            vm.OutputFilename = _filenameFromSaveDialog;
            vm.OutputFolder = _folderFromSaveDialog;
            var expectedDir = Path.Combine(vm.OutputFolder, vm.OutputFilename);

            vm.SaveCommand.Execute(null);

            var interaction = _interactionRequest.AssertWasRaised<MessageInteraction>();
            Assert.AreEqual(_translation.ConfirmSaveAs.ToUpper(), interaction.Title, "Title");
            Assert.AreEqual(_translation.GetFileAlreadyExists(expectedDir), interaction.Text, "Message");
            Assert.AreEqual(MessageIcon.Exclamation, interaction.Icon, "Icon");
            Assert.AreEqual(MessageOptions.YesNo, interaction.Buttons, "Buttons");
        }

        [Test]
        public void SaveCommand_Execute_ProfileIsValid_FilePathNotFromSaveDialog_FileExist_NotifysUser_UserCancels_DoNotCallFinishEvent()
        {
            var vm = BuildViewModel();
            var job = BuildJob(_pdfProfile);
            vm.ExecuteWorkflowStep(job);
            var stepFinishedRaised = false;
            vm.StepFinished += (sender, args) => stepFinishedRaised = true;
            _interactionRequest.Raise(Arg.Do<MessageInteraction>(i => i.Response = MessageResponse.No)); //User cancels
            vm.BrowseFileCommand.Execute(null);
            _file.Exists(Arg.Any<string>()).Returns(true);
            vm.OutputFilename = "not" + _filenameFromSaveDialog;
            vm.OutputFolder = "not" + _folderFromSaveDialog;

            vm.SaveCommand.Execute(null);

            Assert.IsFalse(stepFinishedRaised);
        }

        [Test]
        public void SaveCommand_Execute_ProfileIsValid_FilePathNotFromSaveDialog_FileExist_NotifysUser_UserApplies_CallFinishEvent()
        {
            var vm = BuildViewModel();
            var job = BuildJob(_pdfProfile);
            vm.ExecuteWorkflowStep(job);
            var stepFinishedRaised = false;
            vm.StepFinished += (sender, args) => stepFinishedRaised = true;
            _interactionRequest.Raise(Arg.Do<MessageInteraction>(i => i.Response = MessageResponse.Yes)); //User applies
            vm.BrowseFileCommand.Execute(null);
            _file.Exists(Arg.Any<string>()).Returns(true);
            vm.OutputFilename = "not" + _filenameFromSaveDialog;
            vm.OutputFolder = "not" + _folderFromSaveDialog;

            vm.SaveCommand.Execute(null);

            Assert.IsFalse(stepFinishedRaised);
        }

        [Test]
        public void SaveCommand_Execute_ProfileIsValid_Path_Is_Too_Long()
        {
            var vm = BuildViewModel();
            var job = BuildJob(_pdfProfile);
            vm.ExecuteWorkflowStep(job);

            var stepFinishedRaised = false;
            vm.StepFinished += (sender, args) => stepFinishedRaised = true;

            _interactionRequest.Raise(Arg.Do<MessageInteraction>(i =>
            {
                i.Response = MessageResponse.OK;
            }));

            vm.BrowseFileCommand.Execute(null);
            _file.Exists(Arg.Any<string>()).Returns(false);

            vm.OutputFilename = "PDFCreatorTesfasdfsdfadsfsdfasdfasdfasdtpageaPDFCreatorTestpageaPDFCreatorTestpageaPDFCreatorTestpageaPDFCreatorTestpageaPDFCreatorTestpageaPDFCreatorTestpageaPDFCreatorTestpageaPDFCreatorTestpageaPDFCreatorTestpageaPDFCreatorTestpageaPDFCreatorTestpageaPDFeaPDFCa.pdf";
            vm.OutputFolder = "c:\\";

            vm.SaveCommand.Execute(null);

            Assert.IsFalse(stepFinishedRaised);
        }

        [Test]
        public void SaveCommand_Execute_FolderPathIsInvalid_MessageInteractionIsRaised()
        {
            var vm = BuildViewModel();
            var job = BuildJob(_pdfProfile);
            vm.ExecuteWorkflowStep(job);

            var stepFinishedRaised = false;
            vm.StepFinished += (sender, args) => stepFinishedRaised = true;

            _interactionRequest.Raise(Arg.Do<MessageInteraction>(i =>
            {
                i.Response = MessageResponse.OK;
            }));

            vm.BrowseFileCommand.Execute(null);
            _file.Exists(Arg.Any<string>()).Returns(false);

            vm.OutputFolder = @"c:\<<\";

            vm.SaveCommand.Execute(null);

            Assert.IsFalse(stepFinishedRaised);
        }

        [Test]
        public void SaveCommand_Execute_RootPathIsInvalid_MessageInteractionIsRaised()
        {
            var vm = BuildViewModel();
            var job = BuildJob(_pdfProfile);

            vm.ExecuteWorkflowStep(job);

            var stepFinishedRaised = false;
            vm.StepFinished += (sender, args) => stepFinishedRaised = true;

            _interactionRequest.Raise(Arg.Do<MessageInteraction>(i =>
            {
                i.Response = MessageResponse.OK;
            }));

            vm.BrowseFileCommand.Execute(null);
            _file.Exists(Arg.Any<string>()).Returns(false);

            _pathUtil.IsValidRootedPath(Arg.Any<string>()).Returns(false);

            vm.SaveCommand.Execute(null);

            Assert.IsFalse(stepFinishedRaised);
        }

        [Test]
        public void CancelCommand_Execute_ThrowsAbortWorkflowException()
        {
            var vm = BuildViewModel();
            var job = BuildJob(_pdfProfile);

            vm.ExecuteWorkflowStep(job);

            Assert.Throws<AbortWorkflowException>(() => vm.CancelCommand.Execute(null));
        }

        [Test]
        public void CancelCommand_Execute_CallsFinishedEvent()
        {
            var eventWasRaised = false;
            var vm = BuildViewModel();
            var job = BuildJob(_pdfProfile);

            vm.StepFinished += (sender, args) => eventWasRaised = true;
            vm.ExecuteWorkflowStep(job);

            try
            {
                vm.CancelCommand.Execute(null);
            }
            catch { }

            Assert.IsTrue(eventWasRaised);
        }

        [Test]
        public void MergeCommand_Execute_ThrowsManagePrintJobsException()
        {
            var vm = BuildViewModel();
            var job = BuildJob(_pdfProfile);

            vm.ExecuteWorkflowStep(job);

            Assert.Throws<ManagePrintJobsException>(() => vm.MergeCommand.Execute(null));
        }

        [Test]
        public void MergeCommand_Execute_CallsFinishedEvent()
        {
            var eventWasRaised = false;
            var vm = BuildViewModel();
            var job = BuildJob(_pdfProfile);

            vm.StepFinished += (sender, args) => eventWasRaised = true;
            vm.ExecuteWorkflowStep(job);

            try
            {
                vm.MergeCommand.Execute(null);
            }
            catch { }

            Assert.IsTrue(eventWasRaised);
        }

        [TestCase(0, "")]
        [TestCase(1, "")]
        [TestCase(2, "2")]
        [TestCase(99, "99")]
        [TestCase(100, "99+")]
        public void MergeHint_WithEmptyQueue_Isempty(int numberOfPrintJobs, string expectedHint)
        {
            _jobInfoQueue.Count.Returns(numberOfPrintJobs);
            var vm = BuildViewModel();

            Assert.AreEqual(expectedHint, vm.NumberOfPrintJobsHint);
        }

        [Test]
        public void BrowseFolderCommand_WhenExecuted_OpensWithCurrentFilename()
        {
            var vm = BuildViewModel();
            vm.SetJob(BuildJob(_pdfProfile));

            _saveFileQuery
                .GetFileName(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<OutputFormat>())
                .Returns(new QueryResult<OutputFilenameResult>(false, new OutputFilenameResult("", OutputFormat.Pdf)));

            vm.BrowseFileCommand.Execute(null);

            _saveFileQuery.Received().GetFileName(vm.OutputFolder, vm.OutputFilename, vm.OutputFormat);
        }

        [Test]
        public void BrowseFolderCommand_WhenChangingOutputFormat_SetsViewModelOutputFormat()
        {
            var expectedOutputFormat = OutputFormat.Png;
            var vm = BuildViewModel();
            vm.SetJob(BuildJob(_pdfProfile));

            _saveFileQuery
                .GetFileName(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<OutputFormat>())
                .Returns(x =>
                {
                    var directory = x.ArgAt<string>(0);
                    var filename = x.ArgAt<string>(1);
                    return new QueryResult<OutputFilenameResult>(true, new OutputFilenameResult(Path.Combine(directory, filename), expectedOutputFormat));
                });

            vm.BrowseFileCommand.Execute(null);

            Assert.AreEqual(expectedOutputFormat, vm.OutputFormat);
            Assert.AreEqual(expectedOutputFormat, vm.Job.Profile.OutputFormat);
        }

        [Test]
        public void BrowseFolderCommand_WhenExecutedSuccessfully_SetsOutputFolder()
        {
            var expectedPath = @"Z:\Temp\Folder\Name\test.pdf";
            var vm = BuildViewModel();
            vm.SetJob(BuildJob(_pdfProfile));
            _saveFileQuery
                .GetFileName(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<OutputFormat>())
                .Returns(new QueryResult<OutputFilenameResult>(true, new OutputFilenameResult(expectedPath, OutputFormat.Pdf)));

            vm.BrowseFileCommand.Execute(null);

            Assert.AreEqual(expectedPath, vm.Job.OutputFilenameTemplate);
            StringAssert.StartsWith(expectedPath, vm.Job.OutputFilenameTemplate);
        }

        [Test]
        public void BrowseFolderCommand_WhenCancelled_DoesNotSetOutputFolder()
        {
            var expectedPath = @"X:\";
            var vm = BuildViewModel();
            vm.SetJob(BuildJob(_pdfProfile));
            _saveFileQuery
                .GetFileName(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<OutputFormat>())
                .Returns(new QueryResult<OutputFilenameResult>(false, null));

            vm.BrowseFileCommand.Execute(null);

            Assert.AreEqual(expectedPath, vm.OutputFolder);
            StringAssert.StartsWith(expectedPath, vm.Job.OutputFilenameTemplate);
        }

        [Test]
        public void SetOutputFormatCommand_WithOutputFormat_SetsOutputFormatInJob()
        {
            var expectedOutputFormat = OutputFormat.Jpeg;

            var vm = BuildViewModel();
            var job = BuildJob(_pdfProfile);
            vm.SetJob(job);

            vm.SetOutputFormatCommand.Execute(expectedOutputFormat);

            Assert.AreEqual(vm.OutputFormat, expectedOutputFormat);
            Assert.AreEqual(job.Profile.OutputFormat, expectedOutputFormat);
        }

        [Test]
        public void SetOutputFormatCommand_FilenameWithoutKnownExtension_AddsExtension()
        {
            var vm = BuildViewModel();
            var job = BuildJob(_pdfProfile);
            vm.SetJob(job);
            vm.OutputFilename = "Test.abc";

            vm.SetOutputFormatCommand.Execute(OutputFormat.Jpeg);

            Assert.AreEqual("Test.abc.jpg", vm.OutputFilename);
        }

        [Test]
        public void SetOutputFormatCommand_FilenameWithKnownExtension_SetsExtension()
        {
            var vm = BuildViewModel();
            var job = BuildJob(_pdfProfile);
            vm.SetJob(job);
            vm.OutputFilename = "Test.abc.pdf";

            vm.SetOutputFormatCommand.Execute(OutputFormat.Jpeg);

            Assert.AreEqual("Test.abc.jpg", vm.OutputFilename);
        }

        [Test]
        public void SetOutputFormatCommand_WithOutputFormat_RaisesPropertyChanged()
        {
            var outputFormatChanged = false;

            var vm = BuildViewModel();
            vm.PropertyChanged += (o, a) =>
            {
                if (a.PropertyName == nameof(vm.OutputFormat))
                    outputFormatChanged = true;
            };
            vm.SetJob(BuildJob(_pdfProfile));

            vm.SetOutputFormatCommand.Execute(OutputFormat.Jpeg);

            Assert.IsTrue(outputFormatChanged);
        }

        [Test]
        public void Profiles_WhenProfileIsSelected_OutputFormatIsUpdated()
        {
            var vm = BuildViewModel();
            var job = BuildJob(_pdfProfile);
            vm.SetJob(job);

            var profilesView = CollectionViewSource.GetDefaultView(vm.Profiles);
            profilesView.MoveCurrentToNext();

            Assert.AreEqual(OutputFormat.Png, vm.OutputFormat);
            Assert.AreEqual(OutputFormat.Png, vm.Job.Profile.OutputFormat);
            StringAssert.EndsWith(".png", vm.OutputFilename);
        }

        [Test]
        public void Profiles_UseDeepCopy()
        {
            var vm = BuildViewModel();

            var pdfProfile = vm.Profiles.First(x => x.Guid == _pdfProfile.Guid);
            pdfProfile.Name = "test";

            Assert.AreNotEqual(_pdfProfile.Name, pdfProfile.Name);
            Assert.AreNotSame(_pdfProfile, pdfProfile);
        }

        [Test]
        public void HaveProperOutputFolder_ChangeProfileWithEmptyOutputFolder_FolderDoesNotChange()
        {
            var emptyProfile = new ConversionProfile
            {
                Name = "PDF Profile",
                OutputFormat = OutputFormat.Pdf,
                FileNameTemplate = "X:\\test.pdf",
                TargetDirectory = ""
            };

            _settings.ConversionProfiles.Add(emptyProfile);

            var vm = BuildViewModel();
            var job = BuildJob(_pdfProfile);
            vm.ExecuteWorkflowStep(job);
            vm.OutputFilename = "testFile.pdf";
            vm.OutputFolder = "c:\\Test\\Folder\\";
            vm.SelectedProfile = emptyProfile;

            Assert.AreNotEqual(string.Empty, vm.OutputFolder);
        }

        [Test]
        public void ExecuteWorkflowStep_SetJob_SetsMetadataProperties()
        {
            var title = "newtitle";
            var author = "newauthor";
            var subject = "newsubject";
            var keyword = "newkeyword";

            var vm = BuildViewModel();
            var job = BuildJob(_pdfProfile);

            job.Profile.TitleTemplate = title;
            job.Profile.AuthorTemplate = author;
            job.Profile.SubjectTemplate = subject;
            job.Profile.KeywordTemplate = keyword;

            vm.ExecuteWorkflowStep(job);

            Assert.AreEqual(vm.Title, title);
            Assert.AreEqual(vm.Author, author);
            Assert.AreEqual(vm.Subject, subject);
            Assert.AreEqual(vm.Keyword, keyword);
        }

        [Test]
        public void BrowseFileCommand_CallsDirectoryHelper()
        {
            var vm = BuildViewModel(saveDialogResult: false);
            vm.SetJob(BuildJob(_pdfProfile));
            MockSaveFileDialog(false);

            vm.BrowseFileCommand.Execute(null);

            _directoryHelper.Received(1).CreateDirectory(vm.OutputFolder);
        }
    }
}
