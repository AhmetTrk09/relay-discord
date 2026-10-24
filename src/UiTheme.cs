using System.Windows;
using System.Windows.Markup;
namespace Relay { internal static class UiTheme { internal static ResourceDictionary Create() { return (ResourceDictionary)XamlReader.Parse(@"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
  <Style TargetType='{x:Type TextBlock}'><Setter Property='Foreground' Value='#E8EFF1'/><Setter Property='FontSize' Value='13'/></Style>
  <Style x:Key='Button' TargetType='{x:Type Button}'>
    <Setter Property='Foreground' Value='#D9E5E8'/><Setter Property='Background' Value='#182329'/><Setter Property='BorderBrush' Value='#2B393F'/><Setter Property='BorderThickness' Value='1'/>
    <Setter Property='Padding' Value='11,0'/><Setter Property='MinHeight' Value='32'/><Setter Property='FontSize' Value='12'/><Setter Property='FontWeight' Value='SemiBold'/><Setter Property='Cursor' Value='Hand'/><Setter Property='FocusVisualStyle' Value='{x:Null}'/>
    <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type Button}'>
      <Border x:Name='Body' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='7' Padding='{TemplateBinding Padding}'>
        <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' RecognizesAccessKey='True'/>
      </Border>
      <ControlTemplate.Triggers>
        <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Body' Property='Opacity' Value='0.82'/></Trigger>
        <Trigger Property='IsPressed' Value='True'><Setter TargetName='Body' Property='Opacity' Value='0.6'/></Trigger>
        <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Body' Property='BorderBrush' Value='#A0F3DF'/></Trigger>
        <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.4'/></Trigger>
      </ControlTemplate.Triggers>
    </ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style x:Key='Primary' TargetType='{x:Type Button}' BasedOn='{StaticResource Button}'><Setter Property='Background' Value='#00D4A5'/><Setter Property='BorderBrush' Value='#16E0B6'/><Setter Property='Foreground' Value='#05221B'/></Style>
  <Style x:Key='Danger' TargetType='{x:Type Button}' BasedOn='{StaticResource Button}'><Setter Property='Background' Value='#2A1E28'/><Setter Property='BorderBrush' Value='#4A2838'/><Setter Property='Foreground' Value='#F17C8B'/></Style>
  <Style x:Key='Cyan' TargetType='{x:Type Button}' BasedOn='{StaticResource Button}'><Setter Property='Background' Value='#15282F'/><Setter Property='BorderBrush' Value='#213D45'/><Setter Property='Foreground' Value='#69D5E3'/></Style>
  <Style x:Key='Link' TargetType='{x:Type Button}' BasedOn='{StaticResource Button}'><Setter Property='Foreground' Value='#9BAFB8'/><Setter Property='Background' Value='Transparent'/><Setter Property='BorderBrush' Value='Transparent'/><Setter Property='MinHeight' Value='28'/><Setter Property='Padding' Value='7,0'/><Setter Property='FontSize' Value='11'/></Style>
  <Style x:Key='IconButton' TargetType='{x:Type Button}' BasedOn='{StaticResource Button}'><Setter Property='Padding' Value='0'/><Setter Property='MinHeight' Value='32'/></Style>
  <Style x:Key='Power' TargetType='{x:Type Button}' BasedOn='{StaticResource Button}'>
    <Setter Property='Background' Value='#0D1B1E'/><Setter Property='BorderBrush' Value='#237D69'/><Setter Property='Padding' Value='0'/>
    <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type Button}'><Border x:Name='Circle' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='2' CornerRadius='50'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Circle' Property='Background' Value='#12372E'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Circle' Property='BorderBrush' Value='#A0F3DF'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.4'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style x:Key='Navigation' TargetType='{x:Type RadioButton}'>
    <Setter Property='Foreground' Value='#89999F'/><Setter Property='Cursor' Value='Hand'/><Setter Property='FocusVisualStyle' Value='{x:Null}'/>
    <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type RadioButton}'><Border x:Name='Nav' Background='Transparent' BorderBrush='Transparent' BorderThickness='1' CornerRadius='7' Padding='10,9'><ContentPresenter/></Border><ControlTemplate.Triggers>
      <Trigger Property='IsChecked' Value='True'><Setter Property='Foreground' Value='#00D4A5'/><Setter TargetName='Nav' Property='Background' Value='#192328'/><Setter TargetName='Nav' Property='BorderBrush' Value='#254138'/></Trigger>
      <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Nav' Property='Background' Value='#182126'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Nav' Property='BorderBrush' Value='#75D8C1'/></Trigger>
    </ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style x:Key='ProfileCard' TargetType='{x:Type RadioButton}'>
    <Setter Property='Foreground' Value='#E8EFF1'/><Setter Property='Cursor' Value='Hand'/><Setter Property='FocusVisualStyle' Value='{x:Null}'/>
    <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type RadioButton}'><Border x:Name='Card' Background='#10181D' BorderBrush='#29363D' BorderThickness='1' CornerRadius='10' Padding='16' MinHeight='124'><ContentPresenter/></Border><ControlTemplate.Triggers>
      <Trigger Property='IsChecked' Value='True'><Setter TargetName='Card' Property='Background' Value='#10281F'/><Setter TargetName='Card' Property='BorderBrush' Value='#00B78F'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Card' Property='BorderBrush' Value='#618A7F'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Card' Property='BorderBrush' Value='#A0F3DF'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.4'/></Trigger>
    </ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style TargetType='{x:Type ComboBox}'>
    <Setter Property='Foreground' Value='#E8EFF1'/><Setter Property='Background' Value='#111A1F'/>
    <Setter Property='BorderBrush' Value='#27353B'/><Setter Property='Height' Value='42'/><Setter Property='FontSize' Value='14'/>
    <Setter Property='ScrollViewer.HorizontalScrollBarVisibility' Value='Disabled'/>
    <Setter Property='ScrollViewer.VerticalScrollBarVisibility' Value='Auto'/>
    <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ComboBox}'>
      <Grid>
        <ToggleButton Focusable='False' ClickMode='Press' IsChecked='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'>
          <ToggleButton.Template><ControlTemplate TargetType='{x:Type ToggleButton}'>
            <Border x:Name='Box' Background='#111A1F' BorderBrush='#27353B' BorderThickness='1' CornerRadius='6'>
              <Path Data='M0,0 L4,4 L8,0' Stroke='#54CDB7' StrokeThickness='1.5' HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,16,0'/>
            </Border>
            <ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Box' Property='BorderBrush' Value='#00D4A5'/></Trigger></ControlTemplate.Triggers>
          </ControlTemplate></ToggleButton.Template>
        </ToggleButton>
        <ContentPresenter Margin='14,0,40,0' VerticalAlignment='Center' IsHitTestVisible='False' Content='{TemplateBinding SelectionBoxItem}' ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}'/>
        <Border x:Name='FocusRing' BorderThickness='1' BorderBrush='Transparent' CornerRadius='6' IsHitTestVisible='False'/>
        <Popup x:Name='PART_Popup' Placement='Bottom' AllowsTransparency='True' Focusable='False' IsOpen='{TemplateBinding IsDropDownOpen}'>
          <Border Background='#131E23' BorderBrush='#286A5B' BorderThickness='1' CornerRadius='6' Margin='0,4,0,0' MinWidth='{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}' MaxHeight='{TemplateBinding MaxDropDownHeight}'>
            <ScrollViewer CanContentScroll='True' Margin='4'><ItemsPresenter KeyboardNavigation.DirectionalNavigation='Contained'/></ScrollViewer>
          </Border>
        </Popup>
      </Grid>
      <ControlTemplate.Triggers>
        <Trigger Property='IsKeyboardFocusWithin' Value='True'><Setter TargetName='FocusRing' Property='BorderBrush' Value='#8DEBD5'/></Trigger>
        <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.45'/></Trigger>
      </ControlTemplate.Triggers>
    </ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style TargetType='{x:Type ComboBoxItem}'>
    <Setter Property='Foreground' Value='#E8EFF1'/><Setter Property='Padding' Value='10,9'/>
    <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ComboBoxItem}'>
      <Border x:Name='Item' Background='Transparent' Padding='{TemplateBinding Padding}' CornerRadius='4'><ContentPresenter/></Border>
      <ControlTemplate.Triggers>
        <Trigger Property='IsHighlighted' Value='True'><Setter TargetName='Item' Property='Background' Value='#1D3A34'/></Trigger>
        <Trigger Property='IsSelected' Value='True'><Setter TargetName='Item' Property='Background' Value='#153C31'/></Trigger>
      </ControlTemplate.Triggers>
    </ControlTemplate></Setter.Value></Setter>
  </Style>

  <Style TargetType='{x:Type CheckBox}'>
    <Setter Property='Foreground' Value='#E8EFF1'/><Setter Property='Cursor' Value='Hand'/><Setter Property='FocusVisualStyle' Value='{x:Null}'/>
    <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type CheckBox}'>
      <Grid Background='Transparent'><Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='62'/></Grid.ColumnDefinitions><ContentPresenter Margin='0,0,18,0' RecognizesAccessKey='True'/>
        <Border Grid.Column='1' x:Name='Track' Background='#29373E' BorderBrush='#41545D' BorderThickness='1' CornerRadius='12' Width='38' Height='22' HorizontalAlignment='Right' VerticalAlignment='Center'><Border x:Name='Thumb' Background='#A3B2B9' Width='14' Height='14' CornerRadius='7' HorizontalAlignment='Left' Margin='3,0'/></Border>
      </Grid><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='Track' Property='Background' Value='#00B78F'/><Setter TargetName='Track' Property='BorderBrush' Value='#28DBAF'/><Setter TargetName='Thumb' Property='HorizontalAlignment' Value='Right'/><Setter TargetName='Thumb' Property='Background' Value='#E1FFF7'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Track' Property='BorderBrush' Value='White'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.4'/></Trigger></ControlTemplate.Triggers>
    </ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style TargetType='{x:Type TextBox}'><Setter Property='Background' Value='#0C1317'/><Setter Property='Foreground' Value='#C5D6DC'/><Setter Property='BorderBrush' Value='#26343B'/><Setter Property='Padding' Value='14'/><Setter Property='SelectionBrush' Value='#246452'/><Setter Property='CaretBrush' Value='White'/></Style>
  <Style x:Key='ScrollThumb' TargetType='{x:Type Thumb}'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type Thumb}'><Border Background='#34494F' CornerRadius='3' Margin='3'/></ControlTemplate></Setter.Value></Setter></Style>
  <Style TargetType='{x:Type ScrollBar}'>
    <Setter Property='Width' Value='12'/><Setter Property='Background' Value='#0C1317'/>
    <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ScrollBar}'><Grid Background='{TemplateBinding Background}'><Track x:Name='PART_Track' Orientation='{TemplateBinding Orientation}' IsDirectionReversed='True'><Track.DecreaseRepeatButton><RepeatButton x:Name='PageBefore' Command='ScrollBar.PageUpCommand' Opacity='0' Focusable='False'/></Track.DecreaseRepeatButton><Track.Thumb><Thumb Style='{StaticResource ScrollThumb}'/></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton x:Name='PageAfter' Command='ScrollBar.PageDownCommand' Opacity='0' Focusable='False'/></Track.IncreaseRepeatButton></Track></Grid><ControlTemplate.Triggers><Trigger Property='Orientation' Value='Horizontal'><Setter Property='Width' Value='Auto'/><Setter Property='Height' Value='12'/><Setter TargetName='PART_Track' Property='IsDirectionReversed' Value='False'/><Setter TargetName='PageBefore' Property='Command' Value='ScrollBar.PageLeftCommand'/><Setter TargetName='PageAfter' Property='Command' Value='ScrollBar.PageRightCommand'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
  </Style>
  <DataTemplate x:Key='ActivityRow'>
    <Border Height='38' BorderBrush='#1A252B' BorderThickness='0,1,0,0' Padding='16,0'>
      <Grid VerticalAlignment='Center'><Grid.ColumnDefinitions><ColumnDefinition Width='68'/><ColumnDefinition Width='48'/><ColumnDefinition Width='*'/><ColumnDefinition Width='112'/></Grid.ColumnDefinitions>
        <TextBlock Text='{Binding Time}' FontFamily='Consolas' FontSize='10.5' Foreground='#81969F' VerticalAlignment='Center'/>
        <Border Grid.Column='1' Background='#122B29' CornerRadius='4' Padding='5,4' HorizontalAlignment='Left'><TextBlock Text='{Binding Kind}' Foreground='{Binding Color}' FontFamily='Consolas' FontSize='10'/></Border>
        <TextBlock Grid.Column='2' Text='{Binding Message}' ToolTip='{Binding Message}' FontFamily='Consolas' FontSize='11' Foreground='#D9E5E8' VerticalAlignment='Center' Margin='10,0' TextTrimming='CharacterEllipsis'/>
        <TextBlock Grid.Column='3' Text='{Binding Detail}' Foreground='{Binding Color}' FontFamily='Consolas' FontSize='10' VerticalAlignment='Center' TextAlignment='Right' TextTrimming='CharacterEllipsis'/>
      </Grid>
    </Border>
  </DataTemplate>
</ResourceDictionary>
"); } } }
