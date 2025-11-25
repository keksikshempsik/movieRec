using System;
using System.Windows;
using System.Windows.Controls;
using System.Text.RegularExpressions;
using MovieRecV5.Models;
using MovieRecV5.Services;

namespace MovieRecV5.ViewModels
{
    public partial class Login : Window
    {
        private DatabaseService databaseService;
        private MainWindow mainWindow;

        // Регулярное выражение для проверки email
        private Regex emailRegex = new Regex(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public Login(MainWindow mainWindow)
        {
            InitializeComponent();
            this.mainWindow = mainWindow;
            databaseService = new DatabaseService();

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
                this.Height = 325;
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
            if (string.IsNullOrWhiteSpace(txtLogin.Text))
            {
                throw new Exception("Введите логин");
            }

            if (string.IsNullOrWhiteSpace(txtPassword.Password))
            {
                throw new Exception("Введите пароль");
            }

            if (databaseService.UserExistsByLogin(txtLogin.Text))
            {
                var user = databaseService.GetUserByLogin(txtLogin.Text);
                if (user.Password == User.HashPassword(txtPassword.Password))
                {

                    // Входим пользователя в главном окне
                    mainWindow.LoginUser(user);

                    // Закрываем окно входа
                    this.Close();

                    // ОТКРЫВАЕМ ПРОФИЛЬ ПОСЛЕ УСПЕШНОГО ВХОДА
                    var profileWindow = new UserProfileWindow(user, mainWindow)
                    {
                        Owner = mainWindow,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner
                    };
                    profileWindow.ShowDialog();
                }
                else
                {
                    throw new Exception("Неверный пароль");
                }
            }
            else
            {
                throw new Exception("Пользователь с таким логином не существует");
            }
        }

        private void HandleRegistration()
        {
            // Проверка логина
            if (string.IsNullOrWhiteSpace(txtLogin.Text))
            {
                throw new Exception("Введите логин");
            }

            if (txtLogin.Text.Length < 3)
            {
                throw new Exception("Логин должен содержать минимум 3 символа");
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

            // Проверка существования пользователя
            if (databaseService.UserExistsByLogin(txtLogin.Text))
            {
                throw new Exception("Пользователь с таким логином уже существует");
            }

            // Создаем и добавляем пользователя
            var user = new User
            {
                Login = txtLogin.Text.Trim(),
                Password = User.HashPassword(txtPassword.Password),
                Email = txtEmail.Text.Trim(),
            };

            if (databaseService.AddUser(user))
            {
                // ПОЛУЧАЕМ ПОЛНОГО ПОЛЬЗОВАТЕЛЯ ИЗ БАЗЫ ДАННЫХ (С ID)
                var registeredUser = databaseService.GetUserByLogin(user.Login);

                if (registeredUser != null)
                {

                    // АВТОМАТИЧЕСКИ ЛОГИНИМ ПОЛЬЗОВАТЕЛЯ
                    mainWindow.LoginUser(registeredUser);

                    // Закрываем окно регистрации
                    this.Close();

                    // СРАЗУ ОТКРЫВАЕМ ПРОФИЛЬ ПОЛЬЗОВАТЕЛЯ
                    var profileWindow = new UserProfileWindow(registeredUser, mainWindow)
                    {
                        Owner = mainWindow,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner
                    };
                    profileWindow.ShowDialog();
                }
                else
                {
                    throw new Exception("Ошибка при получении данных пользователя после регистрации");
                }
            }
            else
            {
                throw new Exception("Ошибка при регистрации пользователя");
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