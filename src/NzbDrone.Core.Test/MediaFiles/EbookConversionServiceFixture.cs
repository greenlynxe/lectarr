using System;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Books;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.EbookConversion;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles
{
    [TestFixture]
    public class EbookConversionServiceFixture : CoreTest<EbookConversionService>
    {
        private BookFile _bookFile;
        private LocalBook _localBook;

        [SetUp]
        public void Setup()
        {
            _bookFile = new BookFile
            {
                Id = 1,
                Path = @"C:\Books\Author\Book.mobi".AsOsAgnostic(),
                EditionId = 5
            };

            _localBook = new LocalBook
            {
                Author = new Author { Name = "Some Author" },
                Edition = new Edition { Title = "Some Book", Isbn13 = "9780000000000", Language = "fra" }
            };

            Mocker.GetMock<IConfigService>()
                  .SetupGet(s => s.PreferredBookFormat)
                  .Returns("epub");

            Mocker.GetMock<IConfigService>()
                  .SetupGet(s => s.DeleteOriginalAfterConvert)
                  .Returns(false);

            Mocker.GetMock<IEbookConverter>()
                  .Setup(s => s.IsAvailable())
                  .Returns(true);

            Mocker.GetMock<IEbookConverter>()
                  .Setup(s => s.Convert(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EbookConversionMetadata>()))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FileExists(It.Is<string>(p => p.EndsWith(".epub"))))
                  .Returns(false);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.GetFileSize(It.IsAny<string>()))
                  .Returns(1000);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FileGetLastWrite(It.IsAny<string>()))
                  .Returns(DateTime.UtcNow);
        }

        private void GivenConversionSucceeded()
        {
            Mocker.GetMock<IDiskProvider>()
                  .SetupSequence(s => s.FileExists(It.Is<string>(p => p.EndsWith(".epub"))))
                  .Returns(false)
                  .Returns(true);
        }

        private TrackImportedEvent BuildEvent()
        {
            return new TrackImportedEvent(_localBook, _bookFile, new System.Collections.Generic.List<BookFile>(), true, null);
        }

        [Test]
        public void should_not_convert_when_no_preferred_format()
        {
            Mocker.GetMock<IConfigService>()
                  .SetupGet(s => s.PreferredBookFormat)
                  .Returns(string.Empty);

            Subject.HandleAsync(BuildEvent());

            Mocker.GetMock<IEbookConverter>()
                  .Verify(s => s.Convert(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EbookConversionMetadata>()), Times.Never());
        }

        [Test]
        public void should_not_convert_when_already_preferred_format()
        {
            _bookFile.Path = @"C:\Books\Author\Book.epub".AsOsAgnostic();

            Subject.HandleAsync(BuildEvent());

            Mocker.GetMock<IEbookConverter>()
                  .Verify(s => s.Convert(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EbookConversionMetadata>()), Times.Never());
        }

        [Test]
        public void should_not_convert_audiobooks()
        {
            _bookFile.Path = @"C:\Books\Author\Book.m4b".AsOsAgnostic();

            Subject.HandleAsync(BuildEvent());

            Mocker.GetMock<IEbookConverter>()
                  .Verify(s => s.Convert(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EbookConversionMetadata>()), Times.Never());
        }

        [Test]
        public void should_not_convert_when_converter_unavailable()
        {
            Mocker.GetMock<IEbookConverter>()
                  .Setup(s => s.IsAvailable())
                  .Returns(false);

            Subject.HandleAsync(BuildEvent());

            Mocker.GetMock<IEbookConverter>()
                  .Verify(s => s.Convert(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EbookConversionMetadata>()), Times.Never());

            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public void should_convert_and_add_file_keeping_original()
        {
            GivenConversionSucceeded();

            Subject.HandleAsync(BuildEvent());

            Mocker.GetMock<IEbookConverter>()
                  .Verify(s => s.Convert(_bookFile.Path, It.Is<string>(p => p.EndsWith(".epub")), It.Is<EbookConversionMetadata>(m => m.Language == "fra" && m.Title == "Some Book")), Times.Once());

            Mocker.GetMock<IMediaFileService>()
                  .Verify(s => s.Add(It.Is<BookFile>(f => f.Path.EndsWith(".epub") && f.EditionId == 5)), Times.Once());

            Mocker.GetMock<IMediaFileService>()
                  .Verify(s => s.Delete(It.IsAny<BookFile>(), It.IsAny<DeleteMediaFileReason>()), Times.Never());
        }

        [Test]
        public void should_delete_original_when_configured()
        {
            GivenConversionSucceeded();

            Mocker.GetMock<IConfigService>()
                  .SetupGet(s => s.DeleteOriginalAfterConvert)
                  .Returns(true);

            Subject.HandleAsync(BuildEvent());

            Mocker.GetMock<IRecycleBinProvider>()
                  .Verify(s => s.DeleteFile(_bookFile.Path, It.IsAny<string>()), Times.Once());

            Mocker.GetMock<IMediaFileService>()
                  .Verify(s => s.Delete(_bookFile, DeleteMediaFileReason.Upgrade), Times.Once());
        }

        [Test]
        public void should_not_add_file_when_conversion_fails()
        {
            Mocker.GetMock<IEbookConverter>()
                  .Setup(s => s.Convert(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EbookConversionMetadata>()))
                  .Returns(false);

            Subject.HandleAsync(BuildEvent());

            Mocker.GetMock<IMediaFileService>()
                  .Verify(s => s.Add(It.IsAny<BookFile>()), Times.Never());

            ExceptionVerification.ExpectedWarns(1);
        }
    }
}
