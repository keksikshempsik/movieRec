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

namespace MovieRecV5
{
    /// <summary>
    /// Логика взаимодействия для Login.xaml
    /// </summary>
    public partial class Login : Window
    {
        public Login()
        {
            InitializeComponent();

            // Подписываемся на события изменения состояния RadioButton
            rbLogin.Checked += RbAuthMode_Checked;
            rbRegister.Checked += RbAuthMode_Checked;
        }

        private void RbAuthMode_Checked(object sender, RoutedEventArgs e)
        {
            if (rbLogin.IsChecked == true)
            {
                // Режим входа
                pnlRegister.Visibility = Visibility.Collapsed;
                btnSubmit.Content = "Войти";
                Title = "Вход в систему";

                // Уменьшаем размер окна для режима входа
                this.Height = 275;
            }
            else if (rbRegister.IsChecked == true)
            {
                // Режим регистрации
                pnlRegister.Visibility = Visibility.Visible;
                btnSubmit.Content = "Зарегистрироваться";
                Title = "Регистрация";

                // Увеличиваем размер окна для режима регистрации
                this.Height = 325;
            }
        }

        private void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            // Логика для кнопки (оставлю пустой, как просили)
        }
    }
}