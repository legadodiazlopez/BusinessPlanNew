using Stripe;
using System;
using System.Threading.Tasks;

namespace CrownBuilders.Services
{
    public class StripeService
    {
        public StripeService()
        {
            // Llave de prueba oficial de tu entorno de Stripe
            StripeConfiguration.ApiKey = "sk_test_51PshBvRtS8Sg9vG9XF2M9...";
        }

        /// <summary>
        /// Crea un cliente en Stripe y lo vincula a la suscripción mensual de $15.00 USD.
        /// </summary>
        /// <param name="nombreCompania">Nombre comercial de la empresa</param>
        /// <param name="emailCompania">Correo de contacto para facturación</param>
        /// <param name="telefonoCompania">Teléfono de la compañía</param>
        /// <returns>El ID de la suscripción creada para confirmar el proceso</returns>
        public async Task<string> CrearSuscripcionMensualAsync(string nombreCompania, string emailCompania, string telefonoCompania)
        {
            try
            {
                // PASO 1: Registrar a la compañía como cliente en Stripe
                var opcionesCliente = new CustomerCreateOptions
                {
                    Name = nombreCompania,
                    Email = emailCompania,
                    Phone = telefonoCompania,
                    Description = "Compañía registrada en Crown Builders"
                };

                var servicioCliente = new CustomerService();
                Customer clienteCreado = await servicioCliente.CreateAsync(opcionesCliente);

                // PASO 2: Crear la suscripción mensual usando tu ID de precio ($15.00)
                var opcionesSuscripcion = new SubscriptionCreateOptions
                {
                    Customer = clienteCreado.Id,
                    Items = new System.Collections.Generic.List<SubscriptionItemOptions>
                    {
                        new SubscriptionItemOptions
                        {
                            // Aquí colocamos el ID de precio que obtuvimos de tu panel
                            Price = "price_1Te4R9Do7gaoORCOY1r3f28m",
                        },
                    },

                    // Colecciona el pago automáticamente cobrando a la tarjeta adjunta
                    PaymentBehavior = "default_incomplete",
                };

                // Añadir la configuración para el flujo de autenticación de tarjeta (SCA)
                opcionesSuscripcion.AddExpand("latest_invoice.payment_intent");

                var servicioSuscripcion = new SubscriptionService();
                Subscription suscripcionCreada = await servicioSuscripcion.CreateAsync(opcionesSuscripcion);

                // Retorna el ID de la suscripción para que la app confirme el éxito
                return suscripcionCreada.Id;
            }
            catch (StripeException ex)
            {
                // Captura errores específicos de la API de Stripe
                throw new Exception($"Error en la pasarela de Stripe: {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error general al procesar la suscripción: {ex.Message}");
            }
        }

        /// <summary>
        /// Aplica un saldo a favor de $5.00 USD al cliente en Stripe basado en su correo electrónico.
        /// </summary>
        /// <param name="emailCompania">Correo de la compañía a la que se le descuentan $5.00 en su próxima factura</param>
        /// <returns>True si el balance se aplicó correctamente</returns>
        public async Task<bool> AplicarGarantiaCreditoAsync(string emailCompania)
        {
            try
            {
                var servicioCliente = new CustomerService();
                var opcionesBusqueda = new CustomerListOptions
                {
                    Email = emailCompania,
                    Limit = 1
                };

                var clientes = await servicioCliente.ListAsync(opcionesBusqueda);

                if (clientes.Data.Count == 0)
                {
                    throw new Exception("No se encontró ningún cliente registrado con ese correo en Stripe.");
                }

                var cliente = clientes.Data[0];

                // Un valor NEGATIVO en Balance significa crédito a favor para el siguiente mes.
                // $5.00 USD equivalen a 500 centavos.
                var opcionesActualizar = new CustomerUpdateOptions
                {
                    Balance = cliente.Balance - 500
                };

                await servicioCliente.UpdateAsync(cliente.Id, opcionesActualizar);
                return true;
            }
            catch (StripeException ex)
            {
                throw new Exception($"Error de Stripe al aplicar garantía: {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error general al aplicar garantía: {ex.Message}");
            }
        }
    }
}