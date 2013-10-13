﻿using Simple.MailServer.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace Simple.MailServer.Smtp
{
    public abstract class SmtpCommandParser
    {
        private readonly Dictionary<string, Func<string, string, SmtpResponse>> _commandMapping;

        public bool InDataMode { get; set; }

        protected SmtpCommandParser()
        {
            _commandMapping = CreateCommandMapping();
        }

        private Dictionary<string, Func<string, string, SmtpResponse>> CreateCommandMapping()
        {
            var mapping = new Dictionary<string, Func<string, string, SmtpResponse>>();
            mapping["DATA"] = ProcessCommandDataStart;
            mapping["EHLO"] = ProcessCommandEhlo;
            mapping["HELO"] = ProcessCommandHelo;
            mapping["MAIL FROM:"] = ProcessCommandMailFrom;
            mapping["NOOP"] = ProcessCommandNoop;
            mapping["QUIT"] = ProcessCommandQuit;
            mapping["RCPT TO:"] = ProcessCommandRcptTo;
            mapping["RSET"] = ProcessCommandRset;
            mapping["VRFY"] = ProcessCommandVrfy;
            return mapping;
        }

        protected abstract SmtpResponse ProcessCommandDataStart(string name, string value);
        protected abstract SmtpResponse ProcessCommandDataEnd();
        protected abstract SmtpResponse ProcessCommandEhlo(string name, string value);
        protected abstract SmtpResponse ProcessCommandHelo(string name, string value);
        protected abstract SmtpResponse ProcessCommandMailFrom(string name, string value);
        protected abstract SmtpResponse ProcessCommandNoop(string name, string value);
        protected abstract SmtpResponse ProcessCommandQuit(string name, string value);
        protected abstract SmtpResponse ProcessCommandRcptTo(string name, string value);
        protected abstract SmtpResponse ProcessCommandRset(string name, string value);
        protected abstract SmtpResponse ProcessCommandVrfy(string name, string value);

        protected abstract SmtpResponse ProcessDataLine(byte[] lineBuf);
        protected abstract SmtpResponse ProcessRawLine(string line);

        public SmtpResponse ProcessLineCommand(byte[] lineBuf)
        {
            try
            {
                if (lineBuf.Length > 2040)
                {
                    return new SmtpResponse(500, "Line too long");
                }

                if (InDataMode)
                {
                    if (lineBuf.Length == 1 && lineBuf[0] == '.')
                    {
                        return ProcessCommandDataEnd();
                    }
                    return ProcessDataLine(lineBuf);
                }

                var line = Encoding.UTF8.GetString(lineBuf);

                var rawLineResponse = ProcessRawLine(line);
                if (rawLineResponse != SmtpResponse.None)
                    return rawLineResponse;

                string command = null;
                string arguments = "";

                Func<string, string, SmtpResponse> commandFunc = null;

                var pos = line.IndexOf(':');
                if (pos >= 0)
                {
                    command = line.Substring(0, pos + 1).ToUpperInvariant().Trim();
                    arguments = line.Substring(pos + 1);
                    _commandMapping.TryGetValue(command, out commandFunc);
                }
                if (commandFunc == null)
                {
                    pos = line.IndexOf(' ');
                    if (pos >= 0)
                    {
                        command = line.Substring(0, pos).ToUpperInvariant().Trim();
                        arguments = line.Substring(pos + 1);
                        _commandMapping.TryGetValue(command, out commandFunc);
                    }
                }
                if (command == null)
                {
                    command = line.ToUpperInvariant().Trim();
                    _commandMapping.TryGetValue(command, out commandFunc);
                }

                {
                    if (commandFunc != null)
                    {
                        var response = commandFunc(command, arguments);
                        return response;
                    }

                    return new SmtpResponse(502, "5.5.2 Command not implemented");
                }
            }
            catch (Exception ex)
            {
                MailServerLogger.Instance.Error(ex);
                return new SmtpResponse(500, "Internal Server Error");
            }
        }
    }
}