using System.Windows;
namespace YouTube4KDownloader;
public partial class MainWindow
{
    private List<ClipSelection> selectedClips = new();
    private ClipSelection[] GetClipSelections()
    {
        if(ClipEnabledCheckBox.IsChecked!=true)return Array.Empty<ClipSelection>();
        return selectedClips.Count>0 ? selectedClips.ToArray() : new[]{GetClipSelection()!};
    }
    private void AddSection_Click(object sender,RoutedEventArgs e)
    {
        try
        {
            var clip=GetClipSelection();if(clip==null)return;
            if(selectedClips.Contains(clip))return;
            if(selectedClips.Count>=100)throw new InvalidOperationException(LocalizationManager.Get("SectionLimit"));
            selectedClips.Add(clip); RefreshSectionList();
        }
        catch(Exception ex){MessageBox.Show(ex.Message,LocalizationManager.Get("Section"));}
    }
    private void RemoveSection_Click(object sender,RoutedEventArgs e)
    {
        if(SectionList.SelectedItem is ClipSelection clip)selectedClips.Remove(clip);
        RefreshSectionList();
    }
    private void ClearSections_Click(object sender,RoutedEventArgs e){selectedClips.Clear();RefreshSectionList();}
    private void RefreshSectionList()
    {
        SectionList.ItemsSource=null;SectionList.ItemsSource=selectedClips;
        SectionListPanel.Visibility=selectedClips.Count>0?Visibility.Visible:Visibility.Collapsed;
        ClipCountLabel.Text=selectedClips.Count==0?"":LocalizationManager.Get("Section")+": "+selectedClips.Count;
    }
    private void ClipOption_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        RefreshClipControls();
    }
    private void RefreshClipControls()
    {
        bool enabled = ClipEnabledCheckBox.IsChecked == true;
        ClipEnabledCheckBox.IsEnabled = !_isBusy;
        EncoderComboBox.IsEnabled = !_isBusy;
        AddSectionButton.IsEnabled = RemoveSectionButton.IsEnabled = ClearSectionsButton.IsEnabled = enabled && !_isBusy;
        ClipStartTextBox.IsEnabled = ClipEndTextBox.IsEnabled = enabled && !_isBusy;
        PlaylistCheckBox.IsEnabled = SubtitleCheckBox.IsEnabled = AutoSubtitleCheckBox.IsEnabled = !enabled && !_isBusy;
        if (enabled)
        {
            PlaylistCheckBox.IsChecked = false;
            SubtitleCheckBox.IsChecked = AutoSubtitleCheckBox.IsChecked = false;
        }
    }
    private ClipSelection? GetClipSelection()
    {
        if (ClipEnabledCheckBox.IsChecked != true) return null;
        return new ClipSelection(ClipSelection.ParseTime(ClipStartTextBox.Text), ClipSelection.ParseTime(ClipEndTextBox.Text));
    }
    private void ApplyClipLanguage()
    {
        AddSectionButton.Content="+ "+LocalizationManager.Get("Section");
        RemoveSectionButton.Content=LocalizationManager.Get("Remove");
        ClearSectionsButton.Content=LocalizationManager.Get("Clear");
        EncoderLabel.Text=LocalizationManager.Get("Encoder");
        EncoderComboBox.ToolTip=LocalizationManager.Get("EncoderHint");
        EncoderHintLabel.Text=LocalizationManager.Get("EncoderHint");
        ClipCountLabel.Text=selectedClips.Count==0?"":LocalizationManager.Get("Section")+": "+selectedClips.Count;
        ClipEnabledCheckBox.Content = LocalizationManager.Get("Section");
        ClipStartLabel.Text = LocalizationManager.Get("Start"); ClipEndLabel.Text = LocalizationManager.Get("End");
        ClipFastLabel.Text = LocalizationManager.Get("FastCut");
        ClipHint.Text = "00:01:30 → 00:03:00 · " + LocalizationManager.Get("IndividualOnly");
        ClipFastLabel.ToolTip = LocalizationManager.Get("CutHint");
    }
}
