using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Text.RegularExpressions;

namespace MovieRecV5
{
    /// <summary>
    /// Логика взаимодействия для Login.xaml
    /// </summary>
    public partial class Login : Window
    {
        private DatabaseService databaseService;

        // Регулярное выражение для проверки email
        private Regex emailRegex = new Regex(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public Login()
        {
            InitializeComponent();
            databaseService = new DatabaseService();
            // Подписываемся на события изменения состояния RadioButton
            rbLogin.Checked += RbAuthMode_Checked;
            rbRegister.Checked += RbAuthMode_Checked;
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
                if (btnSubmit.Content == "Войти")
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
                            MessageBox.Show("Вы вошли в аккаунт");
                            // Здесь можно добавить переход на главное окно
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
                else if (btnSubmit.Content == "Зарегистрироваться")
                {
                    // Проверка логина
                    if (string.IsNullOrWhiteSpace(txtLogin.Text))
                    {
                        throw new Exception("Введите логин");
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

                    if (databaseService.UserExistsByLogin(txtLogin.Text))
                    {
                        throw new Exception("Пользователь с таким логином уже существует");
                    }

                    // Создаем и добавляем пользователя
                    var user = new User
                    {
                        Login = txtLogin.Text.Trim(),
                        Password = User.HashPassword(txtPassword.Password),
                        Email = txtEmail.Text.Trim()
                    };

                    if (databaseService.AddUser(user))
                    {
                        MessageBox.Show("Регистрация успешна! Теперь вы можете войти.");
                        // Переключаем на форму входа
                        rbLogin.IsChecked = true;
                    }
                    else
                    {
                        throw new Exception("Ошибка при регистрации пользователя");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}