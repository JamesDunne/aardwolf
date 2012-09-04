using REST0.Definition;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace REST0.APIService
{
    public sealed class LoanHandler : IHttpAsyncHandler
    {
        public Task<IHttpResponseAction> Execute(IHttpRequestContext state)
        {
            if (state.Request.Url.AbsolutePath != "/loan")
                return Task.FromResult<IHttpResponseAction>(new RedirectResponse("/loan"));

            return Task.Run<IHttpResponseAction>(() =>
            {
                //var reply = new StringBuilder(2048 * 1024);
                //var reply = new StringBuilder(180 * 1024);

                string szName = "";
                string[] Months = new string[] { "January","February","March","April","May","June",
                    "July", "August","September","October","November","December" };
                double amount, rate, term, payment, interest, principal, cost;
                int month = 0, year = 1, lastpayment = 1;

                // the form field "names" we want to find values for 
                string Name = "-", Amount = "0", Rate = "0", Term = "0";
                DateTime start = DateTime.Now;

                // get the form field values (note the ending '=' name delimiter)
                Name = state.Request.QueryString["name"];
                Amount = state.Request.QueryString["amount"];
                Rate = state.Request.QueryString["rate"];
                Term = state.Request.QueryString["term"];

                if (String.IsNullOrEmpty(Amount) || String.IsNullOrEmpty(Rate) || String.IsNullOrEmpty(Term))
                    return Task.FromResult<IHttpResponseAction>(new StatusResponse(400, "Bad Request"));

                // all litteral strings provided by a client must be escaped this way
                // if you inject them into an HTML page
                szName = System.Web.HttpUtility.HtmlEncode(Name);

                // filter input data to avoid all the useless/nasty cases
                amount = Double.Parse(Amount);
                if (amount < 1) amount = 1;

                rate = Double.Parse(Rate);
                if (rate > 19) rate = 19;
                else
                    if (rate > 1) rate /= 100;
                    else
                        if (rate < 1) rate = 1 / 100;

                term = Double.Parse(Term);
                if (term < 0.1) term = 1 / 12;
                else
                    if (term > 800) term = 800;

                // calculate the monthly payment amount
                payment = amount * rate / 12 * Math.Pow(1 + rate / 12, term * 12)
                        / (Math.Pow(1 + rate / 12, term * 12) - 1);
                cost = (term * 12 * payment) - amount;

                // build the top of our HTML page
                /*
                reply.Append("<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML 2.0//EN\">"
                       + "<html lang=\"en\"><head><title>Loan Calculator</title><meta http-equiv"
                       + "=\"Content-Type\" content=\"text/html; charset=utf-8\">"
                       + "<link href=\"imgs/style.css\" rel=\"stylesheet\" type=\"text/css\">"
                       + "</head><body><h2>Dear ");
                */

                /*
                if (szName != "" && szName != "-")
                    reply.Append(szName);
                else
                    reply.Append("client");

                reply.Append(", your loan goes as follows:</h2>");
                */

                if (term >= 1)
                    term = Convert.ToInt32(term);
                else
                    term = Math.Ceiling(12 * term);

                /*
                reply.Append("<br><table class=\"clean\" width=240px>"
                      + "<tr><th>loan</th><th>details</th></tr>"
                      + String.Format("<tr class=\"d1\"><td>Amount</td><td>{0:n}</td></tr>", amount)
                      + String.Format("<tr class=\"d0\"><td>Rate</td><td>{0:n}%</td></tr>", rate * 100)
                      + String.Format("<tr class=\"d1\"><td>Term</td><td>{0:n} ", term));

                if (term >= 1)
                    reply.Append("year");
                else
                    reply.Append("month");

                reply.Append("(s)</td></tr>"
                      + String.Format("<tr class=\"d0\"><td>Cost</td><td>{0:n}", cost)
                      + String.Format(" ({0:n}%)</td></tr></table>", 100 / (amount / cost)));

                reply.Append("<br><table class=\"clean\" width=112px>"
                      + String.Format("<tr class=\"d1\"><td><b>YEAR {0:d}</b>", year));

                reply.Append("</td></tr></table><table class=\"clean\" width=550px>"
                      + "<tr><th>month</th><th>payment</th><th>interest</th>"
                      + "<th>principal</th><th>balance</th></tr>");
                */

                for (; ; ) // output monthly payments
                {
                    month++;
                    interest = (amount * rate) / 12;
                    if (amount > payment)
                    {
                        amount = (amount - payment) + interest;
                        principal = payment - interest;
                    }
                    else // calculate last payment
                    {
                        if (lastpayment > 0)
                        {
                            lastpayment = 0;
                            payment = amount;
                            principal = amount - interest;
                            amount = 0;
                        }
                        else // all payments are done, just padd the table
                        {
                            amount = 0;
                            payment = 0;
                            interest = 0;
                            principal = 0;
                        }
                    }

                    /*
                    reply.Append(String.Format("<tr class=\"d{0:d}\">", month & 1)
                          + "<td>" + Months[month - 1] + "</td>"
                          + String.Format("<td>{0:n}</td>", payment)
                          + String.Format("<td>{0:n}</td>", interest)
                          + String.Format("<td>{0:n}</td>", principal)
                          + String.Format("<td>{0:n}</td></tr>", amount));
                    */

                    if (month == 12)
                    {
                        if (amount > 0)
                        {
                            month = 0; year++;
                            /*
                            reply.Append("</table><br><table class=\"clean\" width=112px>"
                                 + "<tr class=\"d1\"><td><b>YEAR " + year + "</b>"
                                 + "</td></tr></table><table class=\"clean\" width=550px>"
                                 + "<tr><th>month</th><th>payment</th><th>interest</th>"
                                 + "<th>principal</th><th>balance</th></tr>");
                            */
                        }
                        else
                            break;
                    }
                }

                TimeSpan elapsed = DateTime.Now - start; // not counting code below

                // time the process and close the HTML page
                /*
                reply.Append("</table><br>This page was generated in "
                      + elapsed.TotalMilliseconds + " milliseconds. <br>(on a"
                      + " 3GHz CPU 1 ms = 3,000,000 cycles)<br></body></html>");
                */

                //return Task.FromResult<IHttpResponseAction>(new ContentResponse(reply.ToString()));
                return Task.FromResult<IHttpResponseAction>(new ContentResponse(String.Format("{0}", elapsed.TotalMilliseconds)));
            });
        }
    }
}
