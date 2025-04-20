using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
namespace Email.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

       public async Task SendEmailAsync(string toEmail, string subject, string body)


{

    Console.WriteLine(toEmail,"c'est la destinatire ");
        Console.WriteLine(subject,"c'est le subject ");
    Console.WriteLine(body,"c'est le body ");

     // Créez une instance du client SMTP
        SmtpClient client = new SmtpClient();

        // Configuration du client SMTP
        client.Host = "smtp.gmail.com"; 
        client.Port = 587;
        client.EnableSsl = true;  // Assurez-vous d'utiliser une connexion sécurisée
        client.Credentials = new NetworkCredential("tassnymelaroussy@gmail.com", "sceueizdnvlacemg");  // Ajoutez vos identifiants
        client.DeliveryMethod = SmtpDeliveryMethod.Network;
        client.UseDefaultCredentials = false;

        // Création du message email
        MailMessage message = new MailMessage();
        message.From = new MailAddress("tassnymelaroussy@gmail.com");
        message.To.Add(toEmail); // Remplacez par l'adresse du destinataire
        message.Subject = subject;
        message.Body = body;


        // Envoi de l'email
        try
        {
            client.Send(message);
            Console.WriteLine("Email envoyé avec succès ! a ");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erreur lors de l'envoi de l'email : " + ex.Message);
        }
}
    }
}