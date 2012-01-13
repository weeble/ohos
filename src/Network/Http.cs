using System;
using System.Collections.Generic;
using System.Text;
using log4net;

namespace OpenHome.Os.Network
{
    class HttpError : Exception
    {
    }

    public enum EMethod
    {
        Options,
        Get,
        Head,
        Post,
        Put,
        Delete,
        Trace,
        Connect,
        Extension
    };

    public enum EVersion
    {
        Http09 = 9,    // HTTP/0.9
        Http10 = 10,   // HTTP/1.0
        Http11 = 11,   // HTTP/1.1
    };

    public class Status
    {
        public uint Code { get; private set; }
        public string Reason { get; private set; }

        public Status(uint aCode, string aReason)
        {
            Code = aCode;
            Reason = aReason;
        }
    }

    class Http
    {
        // Informational codes
        public static readonly Status StatusContinue =  new Status(100, "Continue");
        public static readonly Status StatusSwitchingProtocols =  new Status(101, "Switching Protocols");
        // Success codes
        public static readonly Status StatusOk = new Status(200, "OK");
        public static readonly Status StatusCreated = new Status(201, "Created");
        public static readonly Status StatusAccepted = new Status(202, "Accepted");
        public static readonly Status StatusNonAuthoritativeInformation = new Status(203, "Non-Authoritative Information");
        public static readonly Status StatusNoContent = new Status(204, "No Content");
        public static readonly Status StatusResetContent = new Status(205, "Reset Content");
        public static readonly Status StatusPartialContent = new Status(206, "Partial Content");
        // Redirection codes
        public static readonly Status StatusMultipleChoices =  new Status(300, "Multiple Choices");
        public static readonly Status StatusMovedPermanently =  new Status(301, "Moved Permanently");
        public static readonly Status StatusFound =  new Status(302, "Found");
        public static readonly Status StatusSeeOther =  new Status(303, "See Other");
        public static readonly Status StatusNotModified =  new Status(304, "Not Modified");
        public static readonly Status StatusUseProxy =  new Status(305, "Use Proxy");
        public static readonly Status StatusTemporaryRedirect =  new Status(307, "Temporary Redirect");
        // Client error codes
        public static readonly Status StatusBadRequest =  new Status(400, "Bad Request");
        public static readonly Status StatusUnauthorized =  new Status(401, "Unauthorized");
        public static readonly Status StatusPaymentRequired =  new Status(402, "Payment Required");
        public static readonly Status StatusForbidden =  new Status(403, "Forbidden");
        public static readonly Status StatusNotFound =  new Status(404, "Not Found");
        public static readonly Status StatusMethodNotAllowed =  new Status(405, "Method Not Allowed");
        public static readonly Status StatusNotAcceptable =  new Status(406, "Not Acceptable");
        public static readonly Status StatusProxyAuthenticationRequired =  new Status(407, "Proxy Authentication Required");
        public static readonly Status StatusRequestTimeout =  new Status(408, "Request Timeout");
        public static readonly Status StatusConflict =  new Status(409, "Conflict");
        public static readonly Status StatusGone =  new Status(410, "Gone");
        public static readonly Status StatusLengthRequired =  new Status(411, "Length Required");
        public static readonly Status StatusPreconditionFailed =  new Status(412, "Precondition Failed");
        public static readonly Status StatusRequestEntityTooLarge =  new Status(413, "Request Entity Too Large");
        public static readonly Status StatusRequestUriTooLarge =  new Status(414, "Request URI Too Large");
        public static readonly Status StatusUnsupportedMediaType =  new Status(415, "Unsupported Media Type");
        public static readonly Status StatusRequestedRangeNotSatisfiable =  new Status(416, "Request Range Not Satisfiable");
        public static readonly Status StatusExpectationFailure =  new Status(417, "Expectation Failure");
        // Server error codes
        public static readonly Status StatusInternalServerError =  new Status(500, "Internal Server Error");
        public static readonly Status StatusNotImplemented =  new Status(501, "Not Implemented");
        public static readonly Status StatusBadGateway =  new Status(502, "Bad Gateway");
        public static readonly Status StatusServiceUnavailable =  new Status(503, "Service Unavailable");
        public static readonly Status StatusGatewayTimeout =  new Status(504, "Gateway Timeout");
        public static readonly Status StatusHttpVersionNotSupported = new Status(505, "HTTP Version Not Supported");

        private const string kHttpMethodOptions = "OPTIONS";
        private const string kHttpMethodGet = "GET";
        private const string kHttpMethodHead = "HEAD";
        private const string kHttpMethodPost = "POST";
        private const string kHttpMethodPut = "PUT";
        private const string kHttpMethodDelete = "DELETE";
        private const string kHttpMethodTrace = "TRACE";
        private const string kHttpMethodConnect = "CONNECT";
        private const string kHttpMethodExtension = "EXTENSION";

        private const string kHttpVersion09 = "HTTP/0.9";
        private const string kHttpVersion10 = "HTTP/1.0";
        private const string kHttpVersion11 = "HTTP/1.1";

        public const string kHttpClose = "close";
        public const string kHttpKeepAlive = "Keep-Alive";
        public const string kHttpSpace = " ";
        public const string kHttpNewline = "\r\n";

        public const string kHeaderFieldSeparator = ": ";

        public const string kHeaderGeneralCacheControl = "Cache-Control";
        public const string kHeaderGeneralConnection = "Connection";
        public const string kHeaderGeneralDate = "Date";
        public const string kHeaderGeneralPragma = "Pragma";
        public const string kHeaderGeneralTrailer = "Trailer";
        public const string kHeaderGeneralTransferEncoding = "Transfer-Encoding";
        public const string kHeaderGeneralUpgrade = "Upgrade";
        public const string kHeaderGeneralVia = "Via";
        public const string kHeaderGeneralWarning = "Warning";
        public const string kHeaderRequestAccept = "Accept";
        public const string kHeaderRequestAcceptCharset = "Accept-Charset";
        public const string kHeaderRequestAcceptEncoding = "Accept-Encoding";
        public const string kHeaderRequestAcceptLanguage = "Accept-Language";
        public const string kHeaderRequestAuthorization = "Authorization";
        public const string kHeaderRequestExpect = "Expect";
        public const string kHeaderRequestFrom = "From";
        public const string kHeaderRequestHost = "Host";
        public const string kHeaderRequestIfMatch = "If-Match";
        public const string kHeaderRequestIfModifiedSince = "If-Modified-Since";
        public const string kHeaderRequestIfNoneMatch = "If-None-Match";
        public const string kHeaderRequestIfRange = "If-Range";
        public const string kHeaderRequestIfUnmodifiedSince = "If-Unmodified-Since";
        public const string kHeaderRequestMaxForwards = "Max-Forwards";
        public const string kHeaderRequestProxyAuthorization = "Proxy-Authorization";
        public const string kHeaderRequestRange = "Range";
        public const string kHeaderRequestReferer = "Referer";
        public const string kHeaderRequestTe = "Te";
        public const string kHeaderRequestUserAgent = "User-Agent";
        public const string kHeaderResponseAcceptRanges = "Accept-Ranges";
        public const string kHeaderResponseAge = "Age";
        public const string kHeaderResponseETag = "ETag";
        public const string kHeaderResponseLocation = "Location";
        public const string kHeaderResponseProxyAuthenticate = "Proxy-Authenticate";
        public const string kHeaderResponseRetryAfter = "Retry-After";
        public const string kHeaderResponseServer = "Server";
        public const string kHeaderResponseVary = "Vary";
        public const string kHeaderResponseWwwAuthenticate = "WWW-Authenticate";
        public const string kHeaderEntityAllow = "Allow";
        public const string kHeaderEntityContentEncoding = "Content-Encoding";
        public const string kHeaderEntityContentLanguage = "Content-Language";
        public const string kHeaderEntityContentLength = "Content-Length";
        public const string kHeaderEntityContentLocation = "Content-Location";
        public const string kHeaderEntityContentMd5 = "Content-Md5";
        public const string kHeaderEntityContentRange = "Content-Range";
        public const string kHeaderEntityContentType = "Content-Type";
        public const string kHeaderEntityExpires = "Expires";
        public const string kHeaderEntityLastModified = "Last-Modified";

        public static byte[] ByteArray(string aString)
        {
            return Encoding.UTF8.GetBytes(aString);
        }

        public static EMethod Method(byte[] aMethod)
        {
            if (aMethod == Encoding.UTF8.GetBytes(kHttpMethodOptions))
                return EMethod.Options;
            if (aMethod == Encoding.UTF8.GetBytes(kHttpMethodGet))
                return EMethod.Get;
            if (aMethod == Encoding.UTF8.GetBytes(kHttpMethodHead))
                return EMethod.Head;
            if (aMethod == Encoding.UTF8.GetBytes(kHttpMethodPost))
                return EMethod.Post;
            if (aMethod == Encoding.UTF8.GetBytes(kHttpMethodPut))
                return EMethod.Put;
            if (aMethod == Encoding.UTF8.GetBytes(kHttpMethodDelete))
                return EMethod.Delete;
            if (aMethod == Encoding.UTF8.GetBytes(kHttpMethodTrace))
                return EMethod.Trace;
            if (aMethod == Encoding.UTF8.GetBytes(kHttpMethodConnect))
                return EMethod.Connect;
            if (aMethod == Encoding.UTF8.GetBytes(kHttpMethodExtension))
                return EMethod.Extension;
            throw new HttpError();
        }

        public static string Method(EMethod aMethod)
        {
            switch (aMethod)
            {
                case EMethod.Options: return kHttpMethodOptions;
                case EMethod.Get: return kHttpMethodGet;
                case EMethod.Head: return kHttpMethodHead;
                case EMethod.Post: return kHttpMethodPost;
                case EMethod.Put: return kHttpMethodPut;
                case EMethod.Delete: return kHttpMethodDelete;
                case EMethod.Trace: return kHttpMethodTrace;
                case EMethod.Connect: return kHttpMethodConnect;
                case EMethod.Extension: return kHttpMethodExtension;
                default: break;
            }
            throw new HttpError();
        }

        public static EVersion Version(byte[] aVersion)
        {
            String version = Encoding.UTF8.GetString(aVersion, 0, aVersion.Length);
            if (version == kHttpVersion11)
                return EVersion.Http11;
            if (version == kHttpVersion10)
                return EVersion.Http10;
            if (version == kHttpVersion09)
                return EVersion.Http09;
            throw new HttpError();
        }

        public static string Version(EVersion aVersion)
        {
            switch (aVersion) {
                case EVersion.Http09: return kHttpVersion09;
                case EVersion.Http10: return kHttpVersion10;
                case EVersion.Http11: return kHttpVersion11;
                default: break;
            }
            throw new HttpError();
        }
    }

    public interface IWriterMethod
    {
        void WriteMethod(byte[] aMethod, byte[] aUri, EVersion aVersion);
    }

    public interface IWriterStatus
    {
        void WriteStatus(Status aStatus, EVersion aVersion);
    }

    public interface IWriterHeader : IWriter
    {
        void WriteHeader(byte[] aField, byte[] aValue);
    }

    public interface IWriterHeaderExtended : IWriterHeader 
    {
        IWriterAscii WriteHeaderField(byte[] aField);
        void WriteHeaderTerminator();
    }

    public interface IWriterRequest : IWriterHeader, IWriterMethod
    {
    }

    public interface IWriterResponse : IWriterHeader, IWriterStatus
    {    
    }

    class ReaderRequest
    {
        private readonly IReader iReader;
        private readonly IWriterRequest iWriter;

        public ReaderRequest(IReader aReader, IWriterRequest aWriter)
        {
            iReader = aReader;
            iWriter = aWriter;
        }

        public void Read()
        {
            iReader.ReadFlush();
            uint count = 0;
            while (true)
            {
                byte[] line = Ascii.Trim(iReader.ReadUntil(Ascii.kAsciiLf));
                int bytes = line.Length;
                if (bytes == 0)
                {
                    if (count == 0)
                    {
                        continue; // a blank line before first header - ignore (RFC 2616 section 4.1)
                    }
                    iWriter.WriteFlush();
                    return;
                }
                if (Ascii.IsWhitespace(line[0]))
                    continue; // a line starting with spaces is a continuation line

                Parser parser = new Parser(line);

                if (count == 0)
                { // method
                    byte[] method = parser.Next();
                    byte[] uri = parser.Next();
                    byte[] version = Ascii.Trim(parser.Remaining());
                    iWriter.WriteMethod(method, uri, Http.Version(version));
                }
                else
                { // header
                    byte[] field = parser.Next(Ascii.kAsciiColon);
                    byte[] value = Ascii.Trim(parser.Remaining());
                    iWriter.WriteHeader(field, value);
                }
                count++;
            }
        }
    };

    public interface IHeader
    {
	    void Reset();
	    bool Recognise(byte[] aHeader);
	    void Process(byte[] aValue);
    };

    class ReaderHeader
    {
        private IHeader iHeader;
        private readonly List<IHeader> iHeaders;

        protected ReaderHeader()
        {
            iHeaders = new List<IHeader>();
        }

        protected IHeader Header
        {
            get
            {
    	        return iHeader;
            }
        }

        protected void ResetHeaders()
        {
            foreach (IHeader h in iHeaders)
            {
                h.Reset();
            }
        }

	    protected void ProcessHeader(byte[] aField, byte[] aValue)
        {
            foreach (IHeader h in iHeaders)
            {
                if (h.Recognise(aField))
                {
                    iHeader = h;
                    h.Process(aValue);
                    return;
                }
            }
        }

        public void AddHeader(IHeader aHeader)
        {
            iHeaders.Add(aHeader);
        }
    };

    class ReaderResponse2 : ReaderHeader
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(ReaderResponse2));
        private readonly IReader iReader;

        public EVersion Version { get; private set; }
        public uint Code { get; private set; }
        public byte[] Description { get; private set; }

        public ReaderResponse2(IReader aReader)
        {
            iReader = aReader;
        }

        public void Read()
        {
            iReader.ReadFlush();
            
            uint count = 0;
            
            while (true)
            {
                byte[] line = Ascii.Trim(iReader.ReadUntil(Ascii.kAsciiLf));
                Logger.InfoFormat("ReaderResponse   {0}", Encoding.UTF8.GetString(line, 0, line.Length));
                int bytes = line.Length;
                if (bytes == 0)
                {
                    if (count == 0)
                        continue; // a blank line before first header - ignore (RFC 2616 section 4.1)
                    return;
                }
                if (Ascii.IsWhitespace(line[0]))
                    continue; // a line starting with spaces is a continuation line
            
                Parser parser = new Parser(line);
                if (count == 0)
                {   // status
                    byte[] version = parser.Next();
                    byte[] code = parser.Next();
                    byte[] description = Ascii.Trim(parser.Remaining());
                    ProcessStatus(version, code, description);
                }
                else
                { // header
                    byte[] field = parser.Next(Ascii.kAsciiColon);
                    byte[] value = Ascii.Trim(parser.Remaining());
                    ProcessHeader(field, value);
                }
                count++;
            }
        }

        public void Flush()
        {
            iReader.ReadFlush();
        }

        private void ProcessStatus(byte[] aVersion, byte[] aCode, byte[] aDescription)
        {
            Version = Http.Version(aVersion);
	        try
            {
		        Code = Ascii.Uint(aCode);
	        }
	        catch (AsciiError)
            {
                throw new HttpError();
	        }
        		
            Description = aDescription;
        }
    }

    class ReaderResponse
    {
        private readonly IReader iReader;
        private readonly IWriterResponse iWriter;

        public ReaderResponse(IReader aReader, IWriterResponse aWriter)
        {
            iReader = aReader;
            iWriter = aWriter;
        }

        public void Read()
        {
            iReader.ReadFlush();
            uint count = 0;
            while (true)
            {
                byte[] line = Ascii.Trim(iReader.ReadUntil(Ascii.kAsciiLf));
                int bytes = line.Length;
                if (bytes == 0)
                {
                    if (count == 0)
                        continue; // a blank line before first header - ignore (RFC 2616 section 4.1)
                    iWriter.WriteFlush();
                    return;
                }
                if (Ascii.IsWhitespace(line[0]))
                    continue; // a line starting with spaces is a continuation line
            
                Parser parser = new Parser(line);
                if (count == 0)
                {   // status
                    EVersion version = Http.Version(parser.Next());
                    uint code;
                    try
                    {
                        code = Ascii.Uint(parser.Next());
                    }
                    catch (AsciiError)
                    {
                        throw new HttpError();
                    }

                    byte[] temp = Ascii.Trim(parser.Remaining());
                    string reason = Encoding.UTF8.GetString(temp, 0, temp.Length);
                    Status status =  new Status(code, reason);
                    iWriter.WriteStatus(status, version);
                }
                else
                {   // header
                    byte[] field = parser.Next(Ascii.kAsciiColon);
                    byte[] value = Ascii.Trim(parser.Remaining());
                    iWriter.WriteHeader(field, value);
                }
                count++;
            }
        }
    };

    class WriterHeader : IWriterHeaderExtended
    {
        protected WriterAscii iWriter;

        protected WriterHeader(IWriter aWriter)
        {
            iWriter = new WriterAscii(aWriter);
        }

        public void Write(byte aValue)
        {
            throw new NotImplementedException();
        }

        public void Write(byte[] aBuffer)
        {
            throw new NotImplementedException();
        }

        public void WriteFlush()
        {
            iWriter.WriteNewline();
            iWriter.WriteFlush();
        }

        public void WriteHeader(byte[] aField, byte[] aValue)
        {
            iWriter.Write(aField);
            iWriter.Write(Http.ByteArray(Http.kHeaderFieldSeparator));
            iWriter.Write(aValue);
            iWriter.WriteNewline();
        }

        public IWriterAscii WriteHeaderField(byte[] aField)  // returns a stream for writing the value
        {
            iWriter.Write(aField);
            iWriter.Write(Http.ByteArray(Http.kHeaderFieldSeparator));
            return iWriter;
        }

        public void WriteHeaderTerminator()
        {
            iWriter.WriteNewline();
        }
    }

    class WriterRequest : WriterHeader, IWriterMethod 
    {
        public WriterRequest(IWriter aWriter) : base(aWriter)
        {
        }

        public void WriteMethod(byte[] aMethod, byte[] aUri, EVersion aVersion)
        {
            iWriter.Write(aMethod);
            iWriter.WriteSpace();
            iWriter.Write(aUri);
            iWriter.WriteSpace();
            iWriter.Write(Http.ByteArray(Http.Version(aVersion)));
            iWriter.WriteNewline();
        }
    }

    class WriterResponse : WriterHeader, IWriterStatus 
    {
        public WriterResponse(IWriter aWriter)
            : base(aWriter)
        {
        }

        public void WriteStatus(Status aStatus, EVersion aVersion)
        {
            iWriter.Write(Http.ByteArray(Http.Version(aVersion)));
            iWriter.WriteSpace();
            iWriter.WriteUint(aStatus.Code);
            iWriter.WriteSpace();
            iWriter.Write(Http.ByteArray(aStatus.Reason));
            iWriter.WriteNewline();
        }
    }
}
