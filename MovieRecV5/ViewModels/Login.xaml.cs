using MovieRecV5.Models;
using MovieRecV5.Services;
using Npgsql;
using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace MovieRecV5.ViewModels
{
    public partial class Login : Window
    {
        private PostgresDatabaseService databaseService;
        private MainWindow mainWindow;

        // Регулярное выражение для проверки email
        private Regex emailRegex = new Regex(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public Login(MainWindow mainWindow)
        {
            InitializeComponent();
            this.mainWindow = mainWindow;
            databaseService = new PostgresDatabaseService();

            // Подписываемся на события изменения состояния RadioButton
            rbLogin.Checked += RbAuthMode_Checked;
            rbRegister.Checked += RbAuthMode_Checked;

            // Устанавливаем режим входа по умолчанию
            rbLogin.IsChecked = true;
        }

        private void RbAuthMode_Checked(object sender, RoutedEventArgs e)
        {
            if (rbLogin.IsChecked == true)
            {
                pnlRegister.Visibility = Visibility.Collapsed;
                btnSubmit.Content = "Войти";
                Title = "Вход в систему";
                this.Height = 275;
            }
            else if (rbRegister.IsChecked == true)
            {
                pnlRegister.Visibility = Visibility.Visible;
                btnSubmit.Content = "Зарегистрироваться";
                Title = "Регистрация";
                this.Height = 350; // Увеличено для нового поля
            }
        }

        private bool IsValidEmail(string email)
        {
            return !string.IsNullOrWhiteSpace(email) && emailRegex.IsMatch(email);
        }

        private void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (btnSubmit.Content.ToString() == "Войти")
                {
                    HandleLogin();
                }
                else if (btnSubmit.Content.ToString() == "Зарегистрироваться")
                {
                    HandleRegistration();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void HandleLogin()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtLogin.Text))
                {
                    throw new Exception("Введите логин");
                }

                if (string.IsNullOrWhiteSpace(txtPassword.Password))
                {
                    throw new Exception("Введите пароль");
                }

                var user = databaseService.FindUser(txtLogin.Text, txtPassword.Password);

                if (user != null)
                {

                    mainWindow.LoginUser(user);

                    this.Close();

                    var profileWindow = new UserProfileWindow(user, mainWindow)
                    {
                        Owner = mainWindow,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner
                    };
                    profileWindow.ShowDialog();
                }
                else
                {
                    throw new Exception("Неверный логин или пароль");
                }
            }
            catch (Exception ex)
            {

                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Внутреннее исключение: {ex.InnerException.Message}");
                }

                MessageBox.Show($"Ошибка входа: {ex.Message}", "Ошибка",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void HandleRegistration()
        {
            try
            {
                Console.WriteLine("=== НАЧАЛО РЕГИСТРАЦИИ ===");

                // Проверка логина
                if (string.IsNullOrWhiteSpace(txtLogin.Text))
                {
                    throw new Exception("Введите логин");
                }

                if (txtLogin.Text.Length < 3)
                {
                    throw new Exception("Логин должен содержать минимум 3 символа");
                }

                // После проверки логина добавьте:
                if (databaseService.EmailExists(txtEmail.Text))
                {
                    throw new Exception("Пользователь с таким email уже существует");
                }

                // Проверка отображаемого имени (новое поле)
                if (string.IsNullOrWhiteSpace(txtDisplayName.Text))
                {
                    throw new Exception("Введите отображаемое имя");
                }

                if (txtDisplayName.Text.Length < 2)
                {
                    throw new Exception("Отображаемое имя должно содержать минимум 2 символа");
                }

                // Проверка email
                if (string.IsNullOrWhiteSpace(txtEmail.Text))
                {
                    throw new Exception("Введите email");
                }

                if (!IsValidEmail(txtEmail.Text))
                {
                    throw new Exception("Введите корректный email адрес");
                }

                // Проверка пароля
                if (string.IsNullOrWhiteSpace(txtPassword.Password))
                {
                    throw new Exception("Введите пароль");
                }

                if (txtPassword.Password.Length < 6)
                {
                    throw new Exception("Пароль должен содержать минимум 6 символов");
                }

                Console.WriteLine($"Логин для проверки: '{txtLogin.Text}'");

                // Проверка существования пользователя
                if (databaseService.UserExistsByLogin(txtLogin.Text))
                {
                    throw new Exception("Пользователь с таким логином уже существует");
                }

                // Проверка email на уникальность
                Console.WriteLine("Проверяем email на уникальность...");
                var existingUserByEmail = databaseService.FindUserByLogin(txtEmail.Text); // Замените на метод проверки email, если есть
                if (existingUserByEmail != null && existingUserByEmail.Email == txtEmail.Text)
                {
                    throw new Exception("Пользователь с таким email уже существует");
                }

                Console.WriteLine("Создаем объект пользователя...");
                // Создаем и добавляем пользователя
                var user = new User
                {
                    Login = txtLogin.Text.Trim(),
                    DisplayName = txtDisplayName.Text.Trim(),
                    Password = User.HashPassword(txtPassword.Password),
                    Email = txtEmail.Text.Trim(),
                    AvatarUrl = "default"
                };

                // Отладочная информация
                Console.WriteLine($"Данные пользователя:");
                Console.WriteLine($"  Логин: {user.Login}");
                Console.WriteLine($"  DisplayName: {user.DisplayName}");
                Console.WriteLine($"  Email: {user.Email}");
                Console.WriteLine($"  Password hash: {user.Password}");
                Console.WriteLine($"  AvatarUrl: {user.AvatarUrl}");

                Console.WriteLine("Пытаемся добавить пользователя в базу...");
                if (databaseService.AddUser(user))
                {
                    Console.WriteLine("Пользователь добавлен в базу, получаем данные...");

                    // Получаем пользователя из базы
                    var registeredUser = databaseService.GetUserByLogin(user.Login);

                    if (registeredUser != null)
                    {
                        Console.WriteLine($"Успешная регистрация! ID пользователя: {registeredUser.Id}");

                        // Входим в систему
                        mainWindow.LoginUser(registeredUser);

                        Console.WriteLine("Закрываем окно регистрации...");
                        this.Close();

                        Console.WriteLine("Открываем профиль...");
                        var profileWindow = new UserProfileWindow(registeredUser, mainWindow)
                        {
                            Owner = mainWindow,
                            WindowStartupLocation = WindowStartupLocation.CenterOwner
                        };
                        profileWindow.ShowDialog();

                        Console.WriteLine("=== РЕГИСТРАЦИЯ УСПЕШНО ЗАВЕРШЕНА ===");
                    }
                    else
                    {
                        Console.WriteLine("Ошибка: пользователь не найден после регистрации");
                        throw new Exception("Ошибка при получении данных пользователя после регистрации");
                    }
                }
                else
                {
                    Console.WriteLine("Ошибка: AddUser вернул false");
                    throw new Exception("Ошибка при регистрации пользователя (возможно, пользователь уже существует)");
                }
            }
            catch (PostgresException pgEx)
            {
                Console.WriteLine($"Ошибка PostgreSQL при регистрации:");
                Console.WriteLine($"  SQL State: {pgEx.SqlState}");
                Console.WriteLine($"  Message: {pgEx.Message}");
                Console.WriteLine($"  Detail: {pgEx.Detail}");

                string errorMessage = "Ошибка при регистрации: ";

                // Расшифровка кодов ошибок PostgreSQL
                switch (pgEx.SqlState)
                {
                    case "23505": // unique_violation
                        if (pgEx.Message.Contains("users_login_key"))
                            errorMessage += "Пользователь с таким логином уже существует";
                        else if (pgEx.Message.Contains("users_email_key"))
                            errorMessage += "Пользователь с таким email уже существует";
                        else
                            errorMessage += "Нарушение уникальности данных";
                        break;
                    case "23514": // check_violation
                        errorMessage += "Некорректные данные пользователя";
                        break;
                    case "23502": // not_null_violation
                        errorMessage += "Не заполнены обязательные поля";
                        break;
                    default:
                        errorMessage += pgEx.Message;
                        break;
                }

                MessageBox.Show(errorMessage, "Ошибка регистрации",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка регистрации: {ex.GetType().Name}");
                Console.WriteLine($"Сообщение: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Внутренняя ошибка: {ex.InnerException.Message}");
                }

                MessageBox.Show($"Ошибка регистрации: {ex.Message}", "Ошибка",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Console.WriteLine("=== КОНЕЦ РЕГИСТРАЦИИ ===\n");
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void TxtLogin_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                BtnSubmit_Click(sender, e);
            }
        }

        private void TxtPassword_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                BtnSubmit_Click(sender, e);
            }
        }

        private void TxtEmail_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                BtnSubmit_Click(sender, e);
            }
        }
    }
}